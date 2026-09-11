using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PhotoUploadConcurrencyTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;

    [Fact]
    public async Task KeyedAsyncLock_SameKey_SecondAcquireWaitsForFirstRelease()
    {
        var keyedLock = new KeyedAsyncLock();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("same-key");
            firstEntered.SetResult();
            await releaseFirst.Task;
        });

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("same-key");
            secondEntered.SetResult();
        });

        await Task.Delay(100);
        Assert.False(secondEntered.Task.IsCompleted);

        releaseFirst.SetResult();
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task KeyedAsyncLock_DifferentKeys_DoNotBlockEachOther()
    {
        var keyedLock = new KeyedAsyncLock();
        var enteredA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("key-a");
            enteredA.SetResult();
            await releaseA.Task;
        });

        await enteredA.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var taskB = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("key-b");
            enteredB.SetResult();
        });

        await enteredB.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseA.SetResult();
        await Task.WhenAll(taskA, taskB);
    }

    [Fact]
    public async Task KeyedAsyncLock_CancelWhileWaiting_AllowsNextAcquire()
    {
        var keyedLock = new KeyedAsyncLock();
        var holderReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("cancel-key");
            holderReady.SetResult();
            await releaseFirst.Task;
        });

        await holderReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        var waitTask = keyedLock.AcquireAsync("cancel-key", cts.Token).AsTask();

        await WaitUntilAsync(() => keyedLock.GetParticipantCount("cancel-key") >= 2, TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waitTask);

        releaseFirst.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        await using (var second = await keyedLock.AcquireAsync("cancel-key"))
        {
            Assert.NotNull(second);
            Assert.Equal(1, keyedLock.EntryCount);
        }

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ReleaseOnException_AllowsNextAcquire()
    {
        var keyedLock = new KeyedAsyncLock();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("exc-key");
            throw new InvalidOperationException("test");
        });

        await using var second = await keyedLock.AcquireAsync("exc-key");
        Assert.NotNull(second);
    }

    [Fact]
    public async Task KeyedAsyncLock_EntryRemovedAfterRelease()
    {
        var keyedLock = new KeyedAsyncLock();
        Assert.Equal(0, keyedLock.EntryCount);

        {
            await using var handle = await keyedLock.AcquireAsync("cleanup-key");
            Assert.Equal(1, keyedLock.EntryCount);
        }

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ManyUniqueKeys_DoNotLeaveEntriesAfterRelease()
    {
        var keyedLock = new KeyedAsyncLock();

        for (var i = 0; i < 50; i++)
        {
            await using var handle = await keyedLock.AcquireAsync($"ephemeral-key-{i}");
        }

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ReacquireSameKeyAfterRelease_ReusesCleanDictionary()
    {
        var keyedLock = new KeyedAsyncLock();

        await using (var first = await keyedLock.AcquireAsync("reuse-key"))
        {
            Assert.Equal(1, keyedLock.EntryCount);
        }

        Assert.Equal(0, keyedLock.EntryCount);

        await using var second = await keyedLock.AcquireAsync("reuse-key");
        Assert.Equal(1, keyedLock.EntryCount);
    }

    [Fact]
    public async Task RegisterCandidate_ConcurrentSameIdentity_OnlyOneSavePhotoRunsAtATime()
    {
        var ygsYearId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCount = 0;

        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var password = new Mock<IPasswordService>();
        var tc = new IdentityDocumentValidator(TimeProvider.System);
        var photos = new Mock<IPhotoUploadService>();
        var audit = new Mock<IAuditService>();
        var masking = new Mock<ISensitiveDataMaskingService>();
        var keyedLock = new KeyedAsyncLock();
        var registered = false;

        ygsRepo.Setup(r => r.GetByIdAsync(ygsYearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = ygsYearId });
        userRepo.Setup(r => r.IdentityExistsAsync(It.IsAny<Domain.Enums.IdentityDocumentType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => registered);
        userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        password.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref saveCount);
                await gate.Task;
                return PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg");
            });
        userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid())
            .Callback(() => registered = true);
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new AuthenticationService(
            userRepo.Object, ygsRepo.Object, password.Object, tc, photos.Object, audit.Object, masking.Object,
            keyedLock, TimeProvider.System);

        var model = ValidRegister(ygsYearId);
        var photo = ValidPhoto();

        var first = Task.Run(() => sut.RegisterCandidateAsync(model, photo, null, null));
        var second = Task.Run(() => sut.RegisterCandidateAsync(model, ValidPhoto(), null, null));

        await WaitUntilAsync(() => Volatile.Read(ref saveCount) == 1, TimeSpan.FromSeconds(5));
        Assert.Equal(1, saveCount);

        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, saveCount);
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));
    }

    [Fact]
    public async Task UpdateProfile_ConcurrentPhotoUploads_OnlyOneSavePhotoAtATime()
    {
        var userId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCount = 0;
        var oldPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var photos = new Mock<IPhotoUploadService>();
        var password = new Mock<IPasswordService>();
        var audit = new Mock<IAuditService>();
        var keyedLock = new KeyedAsyncLock();

        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new User
            {
                Id = userId,
                Email = "old@example.com",
                FirstName = "Ali",
                LastName = "Veli",
                PhotoPath = oldPath
            });
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref saveCount);
                await gate.Task;
                return PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg");
            });
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        photos.Setup(p => p.DeletePhotoAsync(oldPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(),
            NullLogger<UserService>.Instance, keyedLock);

        var profile = ValidProfile("old@example.com");
        var first = Task.Run(() => sut.UpdateProfileAsync(userId, profile, ValidPhoto()));
        var second = Task.Run(() => sut.UpdateProfileAsync(userId, profile, ValidPhoto()));

        await WaitUntilAsync(() => Volatile.Read(ref saveCount) == 1, TimeSpan.FromSeconds(5));
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(2, saveCount);
        photos.Verify(p => p.DeletePhotoAsync(oldPath, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void PhotoUploadLockKeys_DoNotContainRawTcOrEmail()
    {
        var key = PhotoUploadLockKeys.ForRegister("10000000146", "ali@example.com");
        Assert.DoesNotContain("10000000146", key, StringComparison.Ordinal);
        Assert.DoesNotContain("ali@example.com", key, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("photo-upload:register:", key, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        throw new TimeoutException("Beklenen koşul zaman aşımına uğradı.");
    }

    private static RegisterViewModel ValidRegister(Guid ygsYearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        TcNo = "10000000146",
        Email = "ali@example.com",
        BirthDate = new DateOnly(2000, 6, 15),
        Phone = "5321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = ygsYearId
    };

    private static ProfileUpdateViewModel ValidProfile(string email) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        Email = email,
        Phone = "5321234567"
    };

    private static PhotoUploadRequest ValidPhoto() => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Length = JpegHeader.Length
    };
}

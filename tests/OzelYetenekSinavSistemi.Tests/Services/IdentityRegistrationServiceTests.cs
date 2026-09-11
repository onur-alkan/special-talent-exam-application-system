using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class IdentityRegistrationServiceTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;
    private static readonly DateOnly PassportExpiry = new(2030, 6, 15);
    private const string LoginFailureMessage = "E-posta/T.C. Kimlik Numarası veya parola hatalı.";

    [Fact]
    public async Task RegisterCandidate_LegacyTcNoModel_DualWritesTurkishIdentity()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = AuthenticationServiceTestSupport.Create(yearId, out var users, out _);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var result = await sut.RegisterCandidateAsync(LegacyRegister(yearId), ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.NotNull(saved);
        Assert.Equal(IdentityDocumentType.TurkishIdentityNumber, saved!.IdentityDocumentType);
        Assert.Equal("10000000146", saved.TcNo);
        Assert.Equal("10000000146", saved.NormalizedIdentityNumber);
        Assert.Equal("TR", saved.NationalityCountryCode);
    }

    [Fact]
    public async Task RegisterCandidate_NewTurkishIdentityModel_DualWrites()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = AuthenticationServiceTestSupport.Create(yearId, out var users, out _);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var model = LegacyRegister(yearId);
        model.TcNo = string.Empty;
        model.IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber;
        model.IdentityNumber = "10000000146";
        model.NationalityCountryCode = "TR";

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.Equal("10000000146", saved!.TcNo);
        Assert.Equal(IdentityDocumentType.TurkishIdentityNumber, saved.IdentityDocumentType);
    }

    [Fact]
    public async Task RegisterCandidate_ForeignIdentity_SetsTcNoNull()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = AuthenticationServiceTestSupport.Create(yearId, out var users, out _);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var model = LegacyRegister(yearId);
        model.TcNo = string.Empty;
        model.IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "DE";

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.Null(saved!.TcNo);
        Assert.Equal("DE", saved.NationalityCountryCode);
        Assert.Equal(IdentityDocumentType.ForeignIdentityNumber, saved.IdentityDocumentType);
    }

    [Fact]
    public async Task RegisterCandidate_Passport_StoresIssuingCountryAndExpiry()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = AuthenticationServiceTestSupport.Create(
            yearId, out var users, out _, timeProvider: new FixedUtcTimeProvider(new DateOnly(2026, 3, 20)));
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var model = LegacyRegister(yearId);
        model.TcNo = string.Empty;
        model.IdentityDocumentType = IdentityDocumentType.Passport;
        model.IdentityNumber = "AB1234567";
        model.NationalityCountryCode = "DE";
        model.IssuingCountryCode = "DE";
        model.PassportExpiryDate = PassportExpiry;

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.Null(saved!.TcNo);
        Assert.Equal("DE", saved.IssuingCountryCode);
        Assert.Equal(PassportExpiry, saved.PassportExpiryDate);
    }

    [Fact]
    public async Task RegisterCandidate_InvalidIdentity_DoesNotSavePhoto()
    {
        var yearId = Guid.NewGuid();
        var sut = AuthenticationServiceTestSupport.Create(yearId, out _, out var photos);

        var model = LegacyRegister(yearId);
        model.TcNo = "12345678901";

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.False(result.Success);
        photos.Verify(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCandidate_DuplicateIdentity_DoesNotSavePhoto()
    {
        var yearId = Guid.NewGuid();
        var sut = AuthenticationServiceTestSupport.Create(yearId, out var users, out var photos);
        users.Setup(r => r.IdentityExistsAsync(
                IdentityDocumentType.TurkishIdentityNumber,
                "10000000146",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.RegisterCandidateAsync(LegacyRegister(yearId), ValidPhoto(), null, null);

        Assert.False(result.Success);
        photos.Verify(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCandidate_DbFailure_DeletesSavedPhoto()
    {
        var yearId = Guid.NewGuid();
        var photoPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";
        var sut = AuthenticationServiceTestSupport.Create(yearId, out var users, out var photos);
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(photoPath));
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));
        photos.Setup(p => p.DeletePhotoAsync(photoPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await sut.RegisterCandidateAsync(LegacyRegister(yearId), ValidPhoto(), null, null);

        Assert.False(result.Success);
        photos.Verify(p => p.DeletePhotoAsync(photoPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateCredentials_TurkishIdentity_Succeeds()
    {
        var user = TurkishUser();
        var sut = CreateLoginSut(user, passwordValid: true);

        var result = await sut.ValidateCredentialsAsync("10000000146", "Passw0rd!", null, null);

        Assert.True(result.Success);
        Assert.Equal(user.Id, result.Data!.Id);
    }

    [Fact]
    public async Task ValidateCredentials_EmailForTurkishUser_Succeeds()
    {
        var user = TurkishUser();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailOrTurkishIdentityAsync("ali@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var sut = CreateLoginSut(user, passwordValid: true, users);

        var result = await sut.ValidateCredentialsAsync("ali@example.com", "Passw0rd!", null, null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateCredentials_EmailForForeignUser_Succeeds()
    {
        var user = ForeignUser();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailOrTurkishIdentityAsync("foreign@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var sut = CreateLoginSut(user, passwordValid: true, users);

        var result = await sut.ValidateCredentialsAsync("foreign@example.com", "Passw0rd!", null, null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateCredentials_ForeignIdentityNumber_IsRejectedWithoutLookup()
    {
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var sut = CreateLoginSut(null, passwordValid: false, users);

        var result = await sut.ValidateCredentialsAsync("99999999999", "Passw0rd!", null, null);

        Assert.False(result.Success);
        Assert.Equal(LoginFailureMessage, result.ErrorMessage);
        users.Verify(
            r => r.GetByEmailOrTurkishIdentityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateCredentials_PassportNumber_IsRejectedWithoutLookup()
    {
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var sut = CreateLoginSut(null, passwordValid: false, users);

        var result = await sut.ValidateCredentialsAsync("AB1234567", "Passw0rd!", null, null);

        Assert.False(result.Success);
        Assert.Equal(LoginFailureMessage, result.ErrorMessage);
        users.Verify(
            r => r.GetByEmailOrTurkishIdentityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateCredentials_UnknownUser_DoesNotRevealExistence()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailOrTurkishIdentityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sut = CreateLoginSut(null, passwordValid: false, users);

        var result = await sut.ValidateCredentialsAsync("10000000146", "wrong", null, null);

        Assert.False(result.Success);
        Assert.Equal(LoginFailureMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateCredentials_LockoutBehavior_IsPreserved()
    {
        var user = TurkishUser();
        user.FailedLoginCount = 4;
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailOrTurkishIdentityAsync("10000000146", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        users.Setup(r => r.UpdateLoginStateAsync(user.Id, 0, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var passwords = new Mock<IPasswordService>();
        passwords.Setup(p => p.Verify(user.PasswordHash, It.IsAny<string>(), out It.Ref<bool>.IsAny)).Returns(false);

        var sut = CreateLoginSut(user, passwordValid: false, users, passwords);

        var result = await sut.ValidateCredentialsAsync("10000000146", "wrong", null, null);

        Assert.False(result.Success);
        users.Verify(r => r.UpdateLoginStateAsync(user.Id, 0, It.IsNotNull<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PasswordReset_ForeignUserFoundByEmail()
    {
        var user = ForeignUser();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByTcNoOrEmailAsync("foreign@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreatePasswordResetSut(users);
        var result = await sut.RequestResetAsync("foreign@example.com", "https://localhost/reset", null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task PasswordReset_TurkishUserFoundByTc()
    {
        var user = TurkishUser();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByTcNoOrEmailAsync("10000000146", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreatePasswordResetSut(users);
        var result = await sut.RequestResetAsync("10000000146", "https://localhost/reset", null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task PasswordReset_PassportIdentifier_ReturnsGenericSuccessWithoutEmail()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByTcNoOrEmailAsync("AB1234567", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var email = new Mock<IEmailService>(MockBehavior.Strict);

        var sut = CreatePasswordResetSut(users, email);
        var result = await sut.RequestResetAsync("AB1234567", "https://localhost/reset", null);

        Assert.True(result.Success);
        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PhotoUploadLockKeys_SamePassportDifferentCountries_ProduceDifferentKeys()
    {
        var keyDe = PhotoUploadLockKeys.ForRegister(
            IdentityDocumentType.Passport, "AB1234567", "DE", "user@example.com");
        var keyFr = PhotoUploadLockKeys.ForRegister(
            IdentityDocumentType.Passport, "AB1234567", "FR", "user@example.com");

        Assert.NotEqual(keyDe, keyFr);
    }

    [Fact]
    public void PhotoUploadLockKeys_SameIdentityAndEmail_ProduceSameKey()
    {
        var first = PhotoUploadLockKeys.ForRegister(
            IdentityDocumentType.ForeignIdentityNumber, "99999999999", null, "user@example.com");
        var second = PhotoUploadLockKeys.ForRegister(
            IdentityDocumentType.ForeignIdentityNumber, "99999999999", null, "user@example.com");

        Assert.Equal(first, second);
    }

    [Fact]
    public void PhotoUploadLockKeys_LegacyWrapper_MatchesTurkishOverload()
    {
        var legacy = PhotoUploadLockKeys.ForRegister("10000000146", "ali@example.com");
        var modern = PhotoUploadLockKeys.ForRegister(
            IdentityDocumentType.TurkishIdentityNumber, "10000000146", null, "ali@example.com");

        Assert.Equal(legacy, modern);
    }

    [Fact]
    public void DatabaseSeeder_InsertIncludesIdentityFields()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "DatabaseSeeder.cs");

        Assert.Contains("IdentityDocumentType", source, StringComparison.Ordinal);
        Assert.Contains("NormalizedIdentityNumber", source, StringComparison.Ordinal);
        Assert.Contains("NationalityCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("@TcNo, 1, @TcNo, @TcNo, 'TR'", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterCandidate_ConcurrentForeignIdentity_OnlyOneSucceeds()
    {
        var yearId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCount = 0;
        var registered = false;

        var users = new Mock<IUserRepository>();
        var photos = new Mock<IPhotoUploadService>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        ygsRepo.Setup(r => r.GetByIdAsync(yearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = yearId });

        users.Setup(r => r.IdentityExistsAsync(
                IdentityDocumentType.ForeignIdentityNumber,
                "99999999999",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => registered);
        users.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref saveCount);
                await gate.Task;
                return PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg");
            });
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid())
            .Callback(() => registered = true);

        var auth = new AuthenticationService(
            users.Object,
            ygsRepo.Object,
            Mock.Of<IPasswordService>(p => p.Hash(It.IsAny<string>()) == "hash"),
            new IdentityDocumentValidator(TimeProvider.System),
            photos.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<ISensitiveDataMaskingService>(m => m.MaskTcNo(It.IsAny<string>()) == "***********"),
            new KeyedAsyncLock(),
            TimeProvider.System);

        var model = LegacyRegister(yearId);
        model.TcNo = string.Empty;
        model.Email = "foreign@example.com";
        model.IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "DE";

        var first = Task.Run(() => auth.RegisterCandidateAsync(model, ValidPhoto(), null, null));
        var second = Task.Run(() => auth.RegisterCandidateAsync(model, ValidPhoto(), null, null));

        await WaitUntilAsync(() => Volatile.Read(ref saveCount) == 1, TimeSpan.FromSeconds(5));
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));
    }

    private static AuthenticationService CreateLoginSut(
        User? user,
        bool passwordValid,
        Mock<IUserRepository>? users = null,
        Mock<IPasswordService>? passwords = null)
    {
        users ??= new Mock<IUserRepository>();
        passwords ??= new Mock<IPasswordService>();
        var audits = new Mock<IAuditService>();
        var masking = new Mock<ISensitiveDataMaskingService>();

        if (user is not null)
        {
            users.Setup(r => r.GetByEmailOrTurkishIdentityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
        }

        var needsRehash = false;
        passwords.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string>(), out needsRehash))
            .Returns(passwordValid);
        passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("new-hash");
        users.Setup(r => r.UpdateLoginStateAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        users.Setup(r => r.UpdatePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        masking.Setup(m => m.MaskEmail(It.IsAny<string>())).Returns("***@example.com");
        audits.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AuthenticationService(
            users.Object,
            Mock.Of<IYgsYearRepository>(),
            passwords.Object,
            new IdentityDocumentValidator(TimeProvider.System),
            Mock.Of<IPhotoUploadService>(),
            audits.Object,
            masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);
    }

    private static PasswordResetService CreatePasswordResetSut(
        Mock<IUserRepository> users,
        Mock<IEmailService>? email = null)
    {
        email ??= new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tokens = new Mock<IPasswordResetTokenRepository>();
        tokens.Setup(t => t.InvalidateActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tokens.Setup(t => t.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        return new PasswordResetService(
            users.Object,
            tokens.Object,
            Mock.Of<IPasswordResetTransactionService>(),
            Mock.Of<IPasswordService>(),
            email.Object,
            new PasswordResetEmailTemplate(),
            Mock.Of<IAuditService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PasswordResetService>.Instance);
    }

    private static User TurkishUser() => new()
    {
        Id = Guid.NewGuid(),
        TcNo = "10000000146",
        Email = "ali@example.com",
        PasswordHash = "hash",
        IsActive = true,
        IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
        NormalizedIdentityNumber = "10000000146"
    };

    private static User ForeignUser() => new()
    {
        Id = Guid.NewGuid(),
        TcNo = null,
        Email = "foreign@example.com",
        PasswordHash = "hash",
        IsActive = true,
        IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber,
        NormalizedIdentityNumber = "99999999999",
        NationalityCountryCode = "DE"
    };

    private static RegisterViewModel LegacyRegister(Guid yearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        TcNo = "10000000146",
        BirthDate = new DateOnly(2000, 6, 15),
        Email = "ali@example.com",
        Phone = "05321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = yearId
    };

    private static PhotoUploadRequest ValidPhoto() => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Length = JpegHeader.Length
    };

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

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateOnly _today;

        public FixedUtcTimeProvider(DateOnly today) => _today = today;

        public override DateTimeOffset GetUtcNow() =>
            new(_today.Year, _today.Month, _today.Day, 12, 0, 0, TimeSpan.Zero);
    }
}

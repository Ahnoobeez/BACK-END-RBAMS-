using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.DTO.Login;
using APINIMIRHEMAA.DTO.Security;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Coordinates the auth controller API endpoints, including request handling and response generation.
    public class AuthController : ControllerBase
    {
        // Database context used by this class to query and persist application records.
        private readonly LocalDbContext _context;
        // Injected or cached jwt service dependency/state used by this class.
        private readonly JwtService _jwtService;
        // Injected or cached passwords dependency/state used by this class.
        private readonly PasswordService _passwords;
        // Injected or cached access permissions dependency/state used by this class.
        private readonly AccessPermissionService _accessPermissions;
        // Injected or cached user sessions dependency/state used by this class.
        private readonly UserSessionService _userSessions;
        // Injected or cached setup token protector dependency/state used by this class.
        private readonly IDataProtector _setupTokenProtector;

        // Initializes a new auth controller instance.
        public AuthController(
            LocalDbContext context,
            JwtService jwtService,
            PasswordService passwords,
            AccessPermissionService accessPermissions,
            UserSessionService userSessions,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _jwtService = jwtService;
            _passwords = passwords;
            _accessPermissions = accessPermissions;
            _userSessions = userSessions;
            _setupTokenProtector = dataProtectionProvider.CreateProtector("APINIMIRHEMAA.SecurityQuestions.Setup");
        }

        /// <summary>Authenticates the supplied username and password, returning a login response or a security-setup challenge.</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // Return HTTP 401 Unauthorized when `user == null || !_passwords.Verify(user.PasswordHash, request.Password)` is true.
            if (user == null || !_passwords.Verify(user.PasswordHash, request.Password))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }
            // Return HTTP 401 Unauthorized when `!user.IsActive` is true.
            if (!user.IsActive)
                return Unauthorized(new { message = "Account Inactive please see the admin." });

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Role_ID == user.Role_ID);
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Department_ID == user.Department_ID);

            // Return HTTP 401 Unauthorized when `role == null || department == null` is true.
            if (role == null || department == null)
                return Unauthorized(new { message = "Account is not fully configured." });

            var answerCount = await _context.UserSecurityAnswers
                .CountAsync(answer => answer.User_ID == user.User_ID);
            // Exit the current operation when `!SecurityQuestionCatalog.HasCompleteAnswerSet(answerCount)` is true.
            if (!SecurityQuestionCatalog.HasCompleteAnswerSet(answerCount))
            {
                return Ok(new
                {
                    securityQuestionsSetupRequired = true,
                    setupToken = CreateSecuritySetupToken(user.User_ID),
                    requiredQuestionCount = SecurityQuestionCatalog.RequiredEnrollmentAnswerCount,
                    questions = SecurityQuestionCatalog.All
                });
            }

            return Ok(await CreateLoginResponseAsync(user, role, department));
        }

        /// <summary>Saves the user's security-question answers and completes sign-in.</summary>
        [HttpPost("complete-security-setup")]
        public async Task<IActionResult> CompleteSecuritySetup([FromBody] CompleteSecuritySetupDto request)
        {
            // Return HTTP 401 Unauthorized when `!TryReadSecuritySetupToken(request.SetupToken, out var userId)` is true.
            if (!TryReadSecuritySetupToken(request.SetupToken, out var userId))
            {
                return Unauthorized(new { message = "The security setup session expired. Sign in again." });
            }
            // Return HTTP 400 Bad Request when `request.Answers == null || request.Answers.Count != SecurityQuestionCatalog.RequiredEnrollmentAnswerCount || request.Answers.Any(answer => answer.QuestionId < 1 || answer.QuestionId > SecurityQuestionCatalog.All.Count || string.IsNullOrWhiteSpace(answer.Answer) || Encoding.UTF8.GetByteCount(SecurityQuestionCatalog.NormalizeAnswer(answer.Answer)) > 72) || request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != SecurityQuestionCatalog.RequiredEnrollmentAnswerCount` is true.
            if (request.Answers == null ||
                request.Answers.Count != SecurityQuestionCatalog.RequiredEnrollmentAnswerCount ||
                request.Answers.Any(answer =>
                    answer.QuestionId < 1 ||
                    answer.QuestionId > SecurityQuestionCatalog.All.Count ||
                    string.IsNullOrWhiteSpace(answer.Answer) ||
                    Encoding.UTF8.GetByteCount(SecurityQuestionCatalog.NormalizeAnswer(answer.Answer)) > 72) ||
                request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != SecurityQuestionCatalog.RequiredEnrollmentAnswerCount)
            {
                return BadRequest(new { message = "Choose and answer 10 different security questions. Answers must be 72 UTF-8 bytes or less." });
            }

            var newAnswers = request.Answers.Select(answer => new UserSecurityAnswer
            {
                User_ID = userId,
                Question_ID = answer.QuestionId,
                AnswerHash = _passwords.Hash(SecurityQuestionCatalog.NormalizeAnswer(answer.Answer!))
            }).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await _context.Users.FirstOrDefaultAsync(account => account.User_ID == userId);
            // Return HTTP 401 Unauthorized when `user == null || !user.IsActive` is true.
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Your account is unavailable. Contact an administrator." });
            }

            var existingAnswerCount = await _context.UserSecurityAnswers
                .CountAsync(answer => answer.User_ID == userId);
            // Return HTTP 409 Conflict when `SecurityQuestionCatalog.HasCompleteAnswerSet(existingAnswerCount)` is true.
            if (SecurityQuestionCatalog.HasCompleteAnswerSet(existingAnswerCount))
            {
                return Conflict(new { message = "Security questions have already been set up. Sign in again." });
            }

            await _context.UserSecurityAnswers
                // Apply this predicate in the database query before materializing results.
                .Where(answer => answer.User_ID == userId)
                // Delete matching rows in the database without loading them into application memory.
                .ExecuteDeleteAsync();
            _context.UserSecurityAnswers.AddRange(newAnswers);
            // Persist the pending entity changes to the database at this point.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var role = await _context.Roles.FirstOrDefaultAsync(item => item.Role_ID == user.Role_ID);
            var department = await _context.Departments.FirstOrDefaultAsync(item => item.Department_ID == user.Department_ID);
            // Return HTTP 401 Unauthorized when `role == null || department == null` is true.
            if (role == null || department == null)
            {
                return Unauthorized(new { message = "Account is not fully configured." });
            }

            return Ok(await CreateLoginResponseAsync(user, role, department));
        }

        /// <summary>Starts password recovery by checking account details and issuing a security-question challenge.</summary>
        [HttpPost("forgot-password/start")]
        public async Task<IActionResult> StartPasswordRecovery([FromBody] ForgotPasswordStartDto request)
        {
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email)` is true.
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Enter both your username and account email." });
            }

            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.UtcNow;
            await _context.PasswordRecoveryChallenges
                // Apply this predicate in the database query before materializing results.
                .Where(challenge => challenge.ExpiresAt <= now || challenge.ResetAt != null)
                // Delete matching rows in the database without loading them into application memory.
                .ExecuteDeleteAsync();

            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(account =>
                    account.Username == username &&
                    account.Email == email &&
                    account.IsActive);
            var questions = SecurityQuestionCatalog.SelectRandomThree();
            // Enter this branch when `user != null` is true.
            if (user != null)
            {
                var savedQuestionIds = await _context.UserSecurityAnswers
                    // Apply this predicate in the database query before materializing results.
                    .Where(answer => answer.User_ID == user.User_ID)
                    .Select(answer => answer.Question_ID)
                    .ToListAsync();
                // Enter this branch when `SecurityQuestionCatalog.HasRecoverableAnswerSet(savedQuestionIds.Count)` is true.
                if (SecurityQuestionCatalog.HasRecoverableAnswerSet(savedQuestionIds.Count))
                {
                    questions = SecurityQuestionCatalog.SelectRandomThree(savedQuestionIds);
                }
                // Use this fallback path when the preceding condition is false.
                else
                {
                    user = null;
                }
            }

            string? employeeName = null;
            // Enter this branch when `user != null` is true.
            if (user != null)
            {
                var employee = await _context.Employee
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Employee_Id == user.Employee_ID);
                employeeName = employee == null
                    ? user.Username
                    : $"{employee.FirstName} {employee.LastName}".Trim();
                // Enter this branch when `string.IsNullOrWhiteSpace(employeeName)` is true.
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    employeeName = user.Username;
                }
            }

            var challenge = new PasswordRecoveryChallenge
            {
                Challenge_ID = Guid.NewGuid(),
                User_ID = user?.User_ID,
                Question1_ID = questions[0].Id,
                Question2_ID = questions[1].Id,
                Question3_ID = questions[2].Id,
                Attempts = 0,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(15)
            };
            _context.PasswordRecoveryChallenges.Add(challenge);
            // Persist the pending entity changes to the database at this point.
            await _context.SaveChangesAsync();

            return Ok(new
            {
                challengeId = challenge.Challenge_ID,
                questions,
                employeeName
            });
        }

        /// <summary>Verifies the password recovery.</summary>
        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> VerifyPasswordRecovery([FromBody] VerifyPasswordRecoveryDto request)
        {
            // Return HTTP 400 Bad Request when `request.Answers == null || request.Answers.Count != 3 || request.Answers.Any(answer => string.IsNullOrWhiteSpace(answer.Answer)) || request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != 3` is true.
            if (request.Answers == null ||
                request.Answers.Count != 3 ||
                request.Answers.Any(answer => string.IsNullOrWhiteSpace(answer.Answer)) ||
                request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != 3)
            {
                return BadRequest(new { message = "Enter an answer for each security question." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var challenge = await _context.PasswordRecoveryChallenges
                .FirstOrDefaultAsync(item => item.Challenge_ID == request.ChallengeId);
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.UtcNow;
            // Return HTTP 400 Bad Request when `challenge == null || challenge.ExpiresAt <= now || challenge.ResetAt != null` is true.
            if (challenge == null || challenge.ExpiresAt <= now || challenge.ResetAt != null)
            {
                return BadRequest(new { message = "This recovery session has expired. Start again." });
            }
            // Exit the current operation when `challenge.VerifiedAt != null` is true.
            if (challenge.VerifiedAt != null)
            {
                return Ok(new { verified = true, attemptsRemaining = Math.Max(0, 3 - challenge.Attempts) });
            }
            // Exit the current operation when `challenge.Attempts >= 3` is true.
            if (challenge.Attempts >= 3)
            {
                return Ok(new { verified = false, attemptsRemaining = 0, message = "No attempts remain. Contact an administrator if account access is blocked." });
            }

            var expectedQuestionIds = new[]
            {
                challenge.Question1_ID,
                challenge.Question2_ID,
                challenge.Question3_ID
            };
            var answerMap = request.Answers.ToDictionary(answer => answer.QuestionId, answer => answer.Answer!);
            var allQuestionsMatch = expectedQuestionIds.All(answerMap.ContainsKey) &&
                answerMap.Keys.All(expectedQuestionIds.Contains);
            var answersAreCorrect = false;
            Users? user = challenge.User_ID is int challengedUserId
                ? await _context.Users.FirstOrDefaultAsync(account => account.User_ID == challengedUserId)
                : null;
            // Enter this branch when `allQuestionsMatch && user != null` is true.
            if (allQuestionsMatch && user != null)
            {
                var savedAnswers = await _context.UserSecurityAnswers
                    // Apply this predicate in the database query before materializing results.
                    .Where(answer => answer.User_ID == user.User_ID &&
                        expectedQuestionIds.Contains(answer.Question_ID))
                    .ToDictionaryAsync(answer => answer.Question_ID);
                answersAreCorrect = user.IsActive &&
                    savedAnswers.Count == expectedQuestionIds.Length &&
                    expectedQuestionIds.All(questionId =>
                        savedAnswers.TryGetValue(questionId, out var savedAnswer) &&
                        _passwords.Verify(
                            savedAnswer.AnswerHash,
                            SecurityQuestionCatalog.NormalizeAnswer(answerMap[questionId])));
            }

            // Enter this branch when `answersAreCorrect` is true.
            if (answersAreCorrect)
            {
                challenge.VerifiedAt = now;
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                challenge.Attempts += 1;
                // Enter this branch when `challenge.Attempts == 3 && user != null` is true.
                if (challenge.Attempts == 3 && user != null)
                {
                    user.IsActive = false;
                }
            }

            // Persist the pending entity changes to the database at this point.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var attemptsRemaining = Math.Max(0, 3 - challenge.Attempts);
            return Ok(new
            {
                verified = answersAreCorrect,
                attemptsRemaining,
                message = answersAreCorrect
                    ? "Security answers verified."
                    : attemptsRemaining == 0
                        ? "No attempts remain. Contact an administrator if account access is blocked."
                        : "One or more answers did not match."
            });
        }

        /// <summary>Resets the password after recovery.</summary>
        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPasswordAfterRecovery([FromBody] ResetPasswordRecoveryDto request)
        {
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmNewPassword)` is true.
            if (string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
            {
                return BadRequest(new { message = "Enter and confirm your new password." });
            }
            // Return HTTP 400 Bad Request when `!PasswordPolicy.IsValid(request.NewPassword)` is true.
            if (!PasswordPolicy.IsValid(request.NewPassword))
            {
                return BadRequest(new { message = PasswordPolicy.RequirementMessage });
            }
            // Return HTTP 400 Bad Request when `request.NewPassword != request.ConfirmNewPassword` is true.
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest(new { message = "The new passwords do not match." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var challenge = await _context.PasswordRecoveryChallenges
                .FirstOrDefaultAsync(item => item.Challenge_ID == request.ChallengeId);
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.UtcNow;
            // Return HTTP 400 Bad Request when `challenge?.User_ID is not int userId || challenge.VerifiedAt == null || challenge.ResetAt != null || challenge.ExpiresAt <= now` is true.
            if (challenge?.User_ID is not int userId ||
                challenge.VerifiedAt == null ||
                challenge.ResetAt != null ||
                challenge.ExpiresAt <= now)
            {
                return BadRequest(new { message = "This verified recovery session is no longer valid. Start again." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(account => account.User_ID == userId);
            // Return HTTP 400 Bad Request when `user == null` is true.
            if (user == null)
            {
                return BadRequest(new { message = "This verified recovery session is no longer valid. Start again." });
            }

            user.PasswordHash = _passwords.Hash(request.NewPassword);
            challenge.ResetAt = now;
            // Persist the pending entity changes to the database at this point.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = user.IsActive
                    ? "Your password was reset. You can now sign in."
                    : "Your password was reset, but an administrator must reactivate your account before you can sign in."
            });
        }

        // Creates the security setup token.
        private string CreateSecuritySetupToken(int userId)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            return _setupTokenProtector.Protect($"{userId}:{expiresAt}");
        }

        // Validates and decrypts a security-setup token and returns its user ID when valid.
        private bool TryReadSecuritySetupToken(string? token, out int userId)
        {
            userId = 0;
            // Exit the current operation when `string.IsNullOrWhiteSpace(token)` is true.
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
            try
            {
                var payload = _setupTokenProtector.Unprotect(token).Split(':');
                return payload.Length == 2 &&
                    int.TryParse(payload[0], out userId) &&
                    long.TryParse(payload[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt) &&
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expiresAt;
            }
            // Handle `CryptographicException` raised by the preceding operation.
            catch (CryptographicException)
            {
                return false;
            }
        }

        // Creates the login response.
        private async Task<object> CreateLoginResponseAsync(Users user, Roles role, Departments department)
        {
            var employee = await _context.Employee
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Employee_Id == user.Employee_ID);
            var employeeName = employee == null
                ? user.Username
                : $"{employee.FirstName} {employee.LastName}".Trim();
            // Enter this branch when `string.IsNullOrWhiteSpace(employeeName)` is true.
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                employeeName = user.Username;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var token = _jwtService.GenerateToken(
                user.User_ID,
                user.Username,
                role.Role_Name,
                department.Department_Name,
                department.Department_ID,
                employeeName,
                sessionId);
            await _userSessions.TouchAsync(user.User_ID, sessionId);
            var permissions = await _accessPermissions.GetEffectivePermissionsAsync(
                user.User_ID, null, null);
            var isSystemAdministrator = ModuleAccess.IsAdministratorRole(role.Role_Name);
            var readableModules = AccessPermissionService.GetReadableModules(
                permissions, isSystemAdministrator);
            var modules = AccessPermissionService.GetAccessibleModules(
                permissions, isSystemAdministrator);

            return new
            {
                token,
                userId = user.User_ID,
                employeeId = user.Employee_ID,
                email = user.Email,
                username = user.Username,
                employeeName,
                role = role.Role_Name,
                department = department.Department_Name,
                modules,
                readableModules
            };
        }

        /// <summary>Retrieves the current access permissions.</summary>
        [Authorize]
        [HttpGet("access-permissions")]
        public async Task<IActionResult> GetCurrentAccessPermissions(CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 401 Unauthorized when `!int.TryParse(userIdValue, out var userId) || userId <= 0` is true.
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            // Return HTTP 401 Unauthorized when `!await _context.Users.AnyAsync(user => user.User_ID == userId, cancellationToken)` is true.
            if (!await _context.Users.AnyAsync(user => user.User_ID == userId, cancellationToken))
                return Unauthorized(new { message = "Your account could not be found. Please sign in again." });

            var permissions = await _accessPermissions.GetEffectivePermissionsAsync(
                userId, null, null, cancellationToken);
            return Ok(permissions);
        }

        /// <summary>Updates the current user's session heartbeat.</summary>
        [Authorize]
        [HttpPost("session-heartbeat")]
        public async Task<IActionResult> SessionHeartbeat(CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 401 Unauthorized when `!int.TryParse(userIdValue, out var userId) || userId <= 0` is true.
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            // Return HTTP 401 Unauthorized when `!await _context.Users.AnyAsync( user => user.User_ID == userId && user.IsActive, cancellationToken)` is true.
            if (!await _context.Users.AnyAsync(
                    user => user.User_ID == userId && user.IsActive,
                    cancellationToken))
            {
                return Unauthorized(new { message = "This account is inactive. Please sign in again." });
            }

            await _userSessions.TouchAsync(
                userId,
                UserSessionService.GetSessionId(User, userId),
                cancellationToken);
            return NoContent();
        }

        /// <summary>Ends the session.</summary>
        [Authorize]
        [HttpPost("session-end")]
        public async Task<IActionResult> EndSession(CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 401 Unauthorized when `!int.TryParse(userIdValue, out var userId) || userId <= 0` is true.
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            await _userSessions.EndAsync(
                userId,
                UserSessionService.GetSessionId(User, userId),
                cancellationToken);
            return NoContent();
        }

        /// <summary>Changes the current user's password after validating the existing password.</summary>
        [Authorize]
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto request)
        {
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmNewPassword)` is true.
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
            {
                return BadRequest(new { message = "Enter your current password and both new password fields." });
            }
            // Return HTTP 400 Bad Request when `!PasswordPolicy.IsValid(request.NewPassword)` is true.
            if (!PasswordPolicy.IsValid(request.NewPassword))
                return BadRequest(new { message = PasswordPolicy.RequirementMessage });
            // Return HTTP 400 Bad Request when `request.NewPassword != request.ConfirmNewPassword` is true.
            if (request.NewPassword != request.ConfirmNewPassword)
                return BadRequest(new { message = "The new passwords do not match." });
            // Return HTTP 400 Bad Request when `request.NewPassword == request.CurrentPassword` is true.
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new { message = "Choose a password different from your current password." });

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 401 Unauthorized when `!int.TryParse(userIdValue, out var userId)` is true.
            if (!int.TryParse(userIdValue, out var userId))
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            var user = _context.Users.FirstOrDefault(account => account.User_ID == userId);
            // Return HTTP 401 Unauthorized when `user == null` is true.
            if (user == null)
                return Unauthorized(new { message = "Your account could not be found. Please sign in again." });
            // Return HTTP 400 Bad Request when `!_passwords.Verify(user.PasswordHash, request.CurrentPassword)` is true.
            if (!_passwords.Verify(user.PasswordHash, request.CurrentPassword))
                return BadRequest(new { message = "Your current password is incorrect." });

            user.PasswordHash = _passwords.Hash(request.NewPassword);
            // Persist the pending entity changes to the database at this point.
            _context.SaveChanges();
            return Ok(new { message = "Your password was changed successfully." });
        }
    }
}
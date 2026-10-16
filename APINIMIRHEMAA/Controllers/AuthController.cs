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
    public class AuthController : ControllerBase
    {
        private readonly LocalDbContext _context;
        private readonly JwtService _jwtService;
        private readonly PasswordService _passwords;
        private readonly IDataProtector _setupTokenProtector;

        public AuthController(
            LocalDbContext context,
            JwtService jwtService,
            PasswordService passwords,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _jwtService = jwtService;
            _passwords = passwords;
            _setupTokenProtector = dataProtectionProvider.CreateProtector("APINIMIRHEMAA.SecurityQuestions.Setup");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !_passwords.Verify(user.PasswordHash, request.Password))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }
            if (!user.IsActive)
                return Unauthorized(new { message = "Account Inactive please see the admin." });

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Role_ID == user.Role_ID);
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Department_ID == user.Department_ID);

            if (role == null || department == null)
                return Unauthorized(new { message = "Account is not fully configured." });

            var answerCount = await _context.UserSecurityAnswers
                .CountAsync(answer => answer.User_ID == user.User_ID);
            if (answerCount != SecurityQuestionCatalog.All.Count)
            {
                return Ok(new
                {
                    securityQuestionsSetupRequired = true,
                    setupToken = CreateSecuritySetupToken(user.User_ID),
                    questions = SecurityQuestionCatalog.All
                });
            }

            return Ok(await CreateLoginResponseAsync(user, role, department));
        }

        [HttpPost("complete-security-setup")]
        public async Task<IActionResult> CompleteSecuritySetup([FromBody] CompleteSecuritySetupDto request)
        {
            if (!TryReadSecuritySetupToken(request.SetupToken, out var userId))
            {
                return Unauthorized(new { message = "The security setup session expired. Sign in again." });
            }
            if (request.Answers == null ||
                request.Answers.Count != SecurityQuestionCatalog.All.Count ||
                request.Answers.Any(answer =>
                    answer.QuestionId < 1 ||
                    answer.QuestionId > SecurityQuestionCatalog.All.Count ||
                    string.IsNullOrWhiteSpace(answer.Answer) ||
                    Encoding.UTF8.GetByteCount(SecurityQuestionCatalog.NormalizeAnswer(answer.Answer)) > 72) ||
                request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != SecurityQuestionCatalog.All.Count)
            {
                return BadRequest(new { message = "Answer all 30 security questions. Answers must be 72 UTF-8 bytes or less." });
            }

            var newAnswers = request.Answers.Select(answer => new UserSecurityAnswer
            {
                User_ID = userId,
                Question_ID = answer.QuestionId,
                AnswerHash = _passwords.Hash(SecurityQuestionCatalog.NormalizeAnswer(answer.Answer!))
            }).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await _context.Users.FirstOrDefaultAsync(account => account.User_ID == userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Your account is unavailable. Contact an administrator." });
            }

            var existingAnswerCount = await _context.UserSecurityAnswers
                .CountAsync(answer => answer.User_ID == userId);
            if (existingAnswerCount == SecurityQuestionCatalog.All.Count)
            {
                return Conflict(new { message = "Security questions have already been set up. Sign in again." });
            }

            await _context.UserSecurityAnswers
                .Where(answer => answer.User_ID == userId)
                .ExecuteDeleteAsync();
            _context.UserSecurityAnswers.AddRange(newAnswers);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var role = await _context.Roles.FirstOrDefaultAsync(item => item.Role_ID == user.Role_ID);
            var department = await _context.Departments.FirstOrDefaultAsync(item => item.Department_ID == user.Department_ID);
            if (role == null || department == null)
            {
                return Unauthorized(new { message = "Account is not fully configured." });
            }

            return Ok(await CreateLoginResponseAsync(user, role, department));
        }

        [HttpPost("forgot-password/start")]
        public async Task<IActionResult> StartPasswordRecovery([FromBody] ForgotPasswordStartDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Enter both your username and account email." });
            }

            var now = DateTime.UtcNow;
            await _context.PasswordRecoveryChallenges
                .Where(challenge => challenge.ExpiresAt <= now || challenge.ResetAt != null)
                .ExecuteDeleteAsync();

            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(account =>
                    account.Username == username &&
                    account.Email == email &&
                    account.IsActive);
            if (user != null)
            {
                var answerCount = await _context.UserSecurityAnswers
                    .CountAsync(answer => answer.User_ID == user.User_ID);
                if (answerCount != SecurityQuestionCatalog.All.Count)
                {
                    user = null;
                }
            }

            var questions = SecurityQuestionCatalog.SelectRandomThree();
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
            await _context.SaveChangesAsync();

            return Ok(new
            {
                challengeId = challenge.Challenge_ID,
                questions
            });
        }

        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> VerifyPasswordRecovery([FromBody] VerifyPasswordRecoveryDto request)
        {
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
            var now = DateTime.UtcNow;
            if (challenge == null || challenge.ExpiresAt <= now || challenge.ResetAt != null)
            {
                return BadRequest(new { message = "This recovery session has expired. Start again." });
            }
            if (challenge.VerifiedAt != null)
            {
                return Ok(new { verified = true, attemptsRemaining = Math.Max(0, 3 - challenge.Attempts) });
            }
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
            if (allQuestionsMatch && user != null)
            {
                var savedAnswers = await _context.UserSecurityAnswers
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

            if (answersAreCorrect)
            {
                challenge.VerifiedAt = now;
            }
            else
            {
                challenge.Attempts += 1;
                if (challenge.Attempts == 3 && user != null)
                {
                    user.IsActive = false;
                }
            }

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

        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPasswordAfterRecovery([FromBody] ResetPasswordRecoveryDto request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
            {
                return BadRequest(new { message = "Enter and confirm your new password." });
            }
            if (request.NewPassword.Length < 8)
            {
                return BadRequest(new { message = "Your new password must be at least 8 characters long." });
            }
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest(new { message = "The new passwords do not match." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var challenge = await _context.PasswordRecoveryChallenges
                .FirstOrDefaultAsync(item => item.Challenge_ID == request.ChallengeId);
            var now = DateTime.UtcNow;
            if (challenge?.User_ID is not int userId ||
                challenge.VerifiedAt == null ||
                challenge.ResetAt != null ||
                challenge.ExpiresAt <= now)
            {
                return BadRequest(new { message = "This verified recovery session is no longer valid. Start again." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(account => account.User_ID == userId);
            if (user == null)
            {
                return BadRequest(new { message = "This verified recovery session is no longer valid. Start again." });
            }

            user.PasswordHash = _passwords.Hash(request.NewPassword);
            challenge.ResetAt = now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = user.IsActive
                    ? "Your password was reset. You can now sign in."
                    : "Your password was reset, but an administrator must reactivate your account before you can sign in."
            });
        }

        private string CreateSecuritySetupToken(int userId)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            return _setupTokenProtector.Protect($"{userId}:{expiresAt}");
        }

        private bool TryReadSecuritySetupToken(string? token, out int userId)
        {
            userId = 0;
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
            catch (CryptographicException)
            {
                return false;
            }
        }

        private async Task<object> CreateLoginResponseAsync(Users user, Roles role, Departments department)
        {
            var employee = await _context.Employee
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Employee_Id == user.Employee_ID);
            var employeeName = employee == null
                ? user.Username
                : $"{employee.FirstName} {employee.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                employeeName = user.Username;
            }

            var token = _jwtService.GenerateToken(
                user.User_ID, user.Username, role.Role_Name, department.Department_Name, employeeName);
            var modules = ModuleAccess.GetModules(role.Role_Name, department.Department_Name);

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
                modules
            };
        }

        [Authorize]
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
            {
                return BadRequest(new { message = "Enter your current password and both new password fields." });
            }
            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "Your new password must be at least 8 characters long." });
            if (request.NewPassword != request.ConfirmNewPassword)
                return BadRequest(new { message = "The new passwords do not match." });
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new { message = "Choose a password different from your current password." });

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            var user = _context.Users.FirstOrDefault(account => account.User_ID == userId);
            if (user == null)
                return Unauthorized(new { message = "Your account could not be found. Please sign in again." });
            if (!_passwords.Verify(user.PasswordHash, request.CurrentPassword))
                return BadRequest(new { message = "Your current password is incorrect." });

            user.PasswordHash = _passwords.Hash(request.NewPassword);
            _context.SaveChanges();
            return Ok(new { message = "Your password was changed successfully." });
        }
    }
}
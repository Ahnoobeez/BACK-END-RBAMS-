namespace APINIMIRHEMAA.DTO
{
    // Defines the user list response or transfer shape exchanged through the API.
    public class UserListDto
    {
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Carries username for the related API operation. This value is optional.
        public string? Username { get; set; }
        // Carries email for the related API operation. This value is optional.
        public string? Email { get; set; }
        // Identifies the related employee record.
        public int Employee_ID { get; set; }
        // Identifies the related role record.
        public int Role_ID { get; set; }
        // Identifies the related department record.
        public int Department_ID { get; set; }
        // Indicates whether active applies to this user list dto.
        public bool IsActive { get; set; }
    }
    // Defines the create user request body exchanged through the API.
    public class CreateUserDto
    {
        // Carries username for the related API operation.
        public string Username { get; set; } = "";
        // Carries email for the related API operation. This value is optional.
        public string? Email { get; set; }
        // Carries password for the related API operation.
        public string Password { get; set; } = "";
        // Identifies the related employee record.
        public int Employee_ID { get; set; }
        // Identifies the related role record.
        public int Role_ID { get; set; }
        // Identifies the related department record.
        public int Department_ID { get; set; }
    }
    // Defines the update user request body exchanged through the API.
    public class UpdateUserDto
    {
        // Carries username for the related API operation.
        public string Username { get; set; } = "";
        // Carries email for the related API operation. This value is optional.
        public string? Email { get; set; }
        // Identifies the related role record.
        public int Role_ID { get; set; }
        // Identifies the related department record.
        public int Department_ID { get; set; }
        // Indicates whether active applies to this update user dto.
        public bool IsActive { get; set; }
    }
    // Defines the reset password response or transfer shape exchanged through the API.
    public class ResetPasswordDto
    {
        // Identifies the related target user record.
        public int TargetUserId { get; set; }
        // Carries admin password for the related API operation.
        public string AdminPassword { get; set; } = "";
    }
    // Defines the delete users response or transfer shape exchanged through the API.
    public class DeleteUsersDto
    {
        // Carries target user ids for the related API operation. This value is optional.
        public List<int>? TargetUserIds { get; set; }
        // Carries admin password for the related API operation.
        public string AdminPassword { get; set; } = "";
    }
    // Defines the change password response or transfer shape exchanged through the API.
    public class ChangePasswordDto
    {
        // Carries current password for the related API operation.
        public string CurrentPassword { get; set; } = "";
        // Carries new password for the related API operation.
        public string NewPassword { get; set; } = "";
        // Carries confirm new password for the related API operation.
        public string ConfirmNewPassword { get; set; } = "";
    }
}
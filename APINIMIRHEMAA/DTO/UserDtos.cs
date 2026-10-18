namespace APINIMIRHEMAA.DTO
{
    public class UserListDto
    {
        public int User_ID { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public int Employee_ID { get; set; }
        public int Role_ID { get; set; }
        public int Department_ID { get; set; }
        public bool IsActive { get; set; }
    }
    public class CreateUserDto
    {
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string Password { get; set; } = "";
        public int Employee_ID { get; set; }
        public int Role_ID { get; set; }
        public int Department_ID { get; set; }
    }
    public class UpdateUserDto
    {
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public int Role_ID { get; set; }
        public int Department_ID { get; set; }
        public bool IsActive { get; set; }
    }
    public class ResetPasswordDto
    {
        public int TargetUserId { get; set; }
        public string AdminPassword { get; set; } = "";
    }
    public class DeleteUsersDto
    {
        public List<int>? TargetUserIds { get; set; }
        public string AdminPassword { get; set; } = "";
    }
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmNewPassword { get; set; } = "";
    }
}
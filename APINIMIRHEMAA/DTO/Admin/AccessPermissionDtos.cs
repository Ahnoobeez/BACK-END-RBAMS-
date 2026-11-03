namespace APINIMIRHEMAA.DTO.Admin
{
    public class AccessPermissionValueDto
    {
        public string ServiceKey { get; set; } = "";
        public string ActionKey { get; set; } = "";
        public bool Allowed { get; set; }
    }

    public class SaveAccessPermissionsDto
    {
        public int? Role_ID { get; set; }
        public int? Department_ID { get; set; }
        public int? User_ID { get; set; }
        public List<AccessPermissionValueDto> Permissions { get; set; } = new();
    }
}

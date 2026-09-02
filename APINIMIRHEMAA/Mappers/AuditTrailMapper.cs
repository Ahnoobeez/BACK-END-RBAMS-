using APINIMIRHEMAA.DTO.AuditTrail;
using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Mappers
{
    public static class AuditTrailMapper
    {
        public static AuditTrailDTO ToAuditTrailDTO(this AuditTrail _AuditTrailModel)
        {
            return new AuditTrailDTO
            {
                AuditTrail_ID = _AuditTrailModel.AuditTrail_ID,
                Name = _AuditTrailModel.Name,
                Date = _AuditTrailModel.Date,
                Time = _AuditTrailModel.Time,
                Action = _AuditTrailModel.Action,
                Department = _AuditTrailModel.Department
            };
        }

    }
}

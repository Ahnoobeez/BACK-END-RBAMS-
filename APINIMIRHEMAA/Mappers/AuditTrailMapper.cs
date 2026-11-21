using APINIMIRHEMAA.DTO.AuditTrail;
using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Mappers
{
    // Converts audit trail mapper data between persistence and API-facing representations.
    public static class AuditTrailMapper
    {
        // Converts to data between persistence and API-facing representations.
        // Maps an audit-trail record to its API DTO.
        public static AuditTrailDTO ToAuditTrailDTO(this AuditTrail _AuditTrailModel)
        {
            return new AuditTrailDTO
            {
                AuditTrail_ID = _AuditTrailModel.AuditTrail_ID,
                Name = _AuditTrailModel.Name,
                Action = _AuditTrailModel.Action,
                Department = _AuditTrailModel.Department
            };
        }

    }
}

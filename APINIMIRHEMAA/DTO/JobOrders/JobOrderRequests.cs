using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.JobOrders
{
    // Defines the create job order request body exchanged through the API.
    public class CreateJobOrderRequest
    {
        [Range(1, int.MaxValue)]
        // Carries conforme file id for the related API operation. The range annotation constrains its numeric value.
        public int Conforme_FileID { get; set; }
        // Supplies the title used to identify the related record.
        public string? Title { get; set; }
        // Records the installation date for this operation.
        public DateOnly? Installation_Date { get; set; }
        [Required]
        // Carries target delivery for the related API operation. This value is optional. The required annotation rejects a missing value.
        public DateOnly? Target_Delivery { get; set; }
        // Records the date delivered for this operation.
        public DateOnly? Date_Delivered { get; set; }
        // Carries tiling for the related API operation. This value is optional.
        public bool? Tiling { get; set; }
        // Carries eyelet for the related API operation. This value is optional.
        public bool? Eyelet { get; set; }
        // Carries bleeding for the related API operation. This value is optional.
        public bool? Bleeding { get; set; }
        // Current workflow status for this create job order request.
        public string? Status { get; set; }
        [MaxLength(4000)]
        // Carries special instruction for the related API operation. This value is optional. The maximum-length annotation limits accepted text length.
        public string? SpecialInstruction { get; set; }
    }

    // Defines the add job order comment request body exchanged through the API.
    public class AddJobOrderCommentRequest
    {
        [Range(1, int.MaxValue)]
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }
        [Required, MaxLength(4000)]
        // Free-text comment that records context or explanations for this add job order comment request.
        public string Comment { get; set; } = "";
    }
}

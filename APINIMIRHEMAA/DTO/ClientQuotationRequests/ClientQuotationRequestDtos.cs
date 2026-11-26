using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace APINIMIRHEMAA.DTO.ClientQuotationRequests
{
    public sealed class CreateClientQuotationRequestFormDto
    {
        [Required]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\+63[1-9]\d{8,9}$")]
        public string Telephone { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(RB|COD|30 days|60 days|90 days|120 days)$")]
        public string PaymentTerms { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string BusinessStyle { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{3}-\d{3}-\d{3}(-\d{3})?$")]
        public string Tin { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string CompanyAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string TermsAndCondition { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ProjectTitle { get; set; } = string.Empty;

        [Required]
        [MaxLength(300000)]
        public string ItemsJson { get; set; } = string.Empty;

        public List<IFormFile> Graphics { get; set; } = new();
    }

    public sealed class CreateClientQuotationRequestDto
    {
        [Required]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\+63[1-9]\d{8,9}$")]
        public string Telephone { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(RB|COD|30 days|60 days|90 days|120 days)$")]
        public string PaymentTerms { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string BusinessStyle { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{3}-\d{3}-\d{3}(-\d{3})?$")]
        public string Tin { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string CompanyAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string TermsAndCondition { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ProjectTitle { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<ClientQuotationRequestItemDto> Items { get; set; } = new();
    }

    public sealed class ClientQuotationRequestItemDto
    {
        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Requirement { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "100000")]
        public decimal Width { get; set; }

        [Range(typeof(decimal), "0.01", "100000")]
        public decimal Height { get; set; }

        [Required]
        [MaxLength(8)]
        public string Measurement { get; set; } = string.Empty;

        [Range(typeof(decimal), "1", "100000")]
        public decimal Quantity { get; set; }
    }

    public sealed class ClientQuotationRequestResponseDto
    {
        public int RequestId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string BusinessStyle { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string Tin { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string PaymentTerms { get; set; } = string.Empty;
        public string TermsAndCondition { get; set; } = string.Empty;
        public string ProjectTitle { get; set; } = string.Empty;
        public List<ClientQuotationRequestItemResponseDto> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal DeliveryCharge { get; set; }
        public decimal InstallationCharge { get; set; }
        public decimal Vat { get; set; }
        public decimal OverallTotal { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ClaimedByUserId { get; set; }
        public DateTime? ClaimedAtUtc { get; set; }
    }

    public sealed class ClientQuotationRequestItemResponseDto
    {
        public string Branch { get; set; } = string.Empty;
        public string Requirement { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public string Measurement { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

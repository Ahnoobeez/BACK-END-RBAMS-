namespace APINIMIRHEMAA.DTO.QuotationItems
{
    public class AddQuotationItemDto
    {
        public string? Requirements { get; set; }
        public string? Description { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public string? Measurement_Unit { get; set; }
        public decimal? Price_Per_Square_Inch { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Unit_Price { get; set; }
        public decimal? Total_Amount { get; set; }
        public int QS_ID { get; set; }
        public List<AddQuotationItemMaterialDto> Materials { get; set; } = new();
    }

    public class AddQuotationItemMaterialDto
    {
        public int Material_ID { get; set; }
    }
}

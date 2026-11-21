using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace APINIMIRHEMAA.Data
{
    // Configures database access and entity mapping for encrypted data mapping data.
    internal static class EncryptedDataMapping
    {
        // Registers encryption converters for the client fields stored in protected form.
        public static void ConfigureClients(ModelBuilder modelBuilder, EncryptionService encryptionService)
        {
            var client = modelBuilder.Entity<ClientEntity>();
            ConfigureEncryptedString(client.Property(item => item.CreatedBy), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Client_Name), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Client_Telephone), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Contact_Person), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Business_Style), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Client_EmailAddress), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.TermsAndCondition), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Company_Address), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.TIN), encryptionService);
            ConfigureEncryptedString(client.Property(item => item.Payment_Terms), encryptionService);
            client.Property(item => item.Price_Per_Square_Inch)
                .HasConversion(new ValueConverter<decimal, string>(
                    value => encryptionService.Encrypt(value.ToString(CultureInfo.InvariantCulture)),
                    value => decimal.Parse(
                        encryptionService.TryDecrypt(value),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture)))
                .HasColumnType("nvarchar(max)");
        }

        // Configures database access and entity mapping for fields data.
        // Registers encryption converters for protected employee and local record fields.
        public static void ConfigureLocalRecords(ModelBuilder modelBuilder, EncryptionService encryptionService)
        {
            var employee = modelBuilder.Entity<Employee>();
            ConfigureEncryptedString(employee.Property(item => item.LastName), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.FirstName), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.MiddleName), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.status), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.ContactNumber), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.SSS_Number), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.Philhealth_Number), encryptionService);
            ConfigureEncryptedString(employee.Property(item => item.Pagibig_Number), encryptionService);
            employee.Property(item => item.Birthday)
                .HasConversion(CreateDateConverter(encryptionService))
                .HasColumnType("nvarchar(max)");
            employee.Property(item => item.Date_Started)
                .HasConversion(CreateDateConverter(encryptionService))
                .HasColumnType("nvarchar(max)");
            employee.Property(item => item.Date_Resigned)
                .HasConversion(CreateDateConverter(encryptionService))
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<UserSignature>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserSignature");
                entity.HasKey(signature => signature.User_ID);
                entity.Property(signature => signature.SignatureData)
                    .HasConversion(CreateByteArrayConverter(encryptionService))
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
            });

            modelBuilder.Entity<EmployeeProfilePhoto>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.Property(photo => photo.PhotoData)
                    .HasConversion(CreateByteArrayConverter(encryptionService))
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
            });

            modelBuilder.Entity<UserProfilePhoto>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserProfilePhoto");
                entity.HasKey(photo => photo.User_ID);
                entity.Property(photo => photo.PhotoData)
                    .HasConversion(CreateByteArrayConverter(encryptionService))
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
            });
        }

        // Configures the encrypted string.
        private static void ConfigureEncryptedString(
            PropertyBuilder<string?> property,
            EncryptionService encryptionService)
        {
            property
                .HasConversion(new ValueConverter<string?, string?>(
                    value => encryptionService.Encrypt(value),
                    value => encryptionService.TryDecrypt(value)))
                .HasColumnType("nvarchar(max)")
                .HasAnnotation("MaxLength", null);
        }

        // Creates the date converter.
        private static ValueConverter<DateOnly?, string?> CreateDateConverter(
            EncryptionService encryptionService)
        {
            return new ValueConverter<DateOnly?, string?>(
                value => value.HasValue
                    ? encryptionService.Encrypt(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    : null,
                value => string.IsNullOrEmpty(value)
                    ? null
                    : DateOnly.Parse(
                        encryptionService.TryDecrypt(value),
                        CultureInfo.InvariantCulture));
        }

        // Creates the byte array converter.
        private static ValueConverter<byte[], byte[]> CreateByteArrayConverter(
            EncryptionService encryptionService)
        {
            return new ValueConverter<byte[], byte[]>(
                value => encryptionService.Encrypt(value),
                value => encryptionService.TryDecrypt(value));
        }
    }
}

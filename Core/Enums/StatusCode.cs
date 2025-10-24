namespace Core.Enums
{
    public enum StatusCode
    {
        Ok = 200,
        Created = 201,
        NoContent = 204,
        BadRequest = 400,
        NotFound = 404,
        Conflict = 409,
        ValidationError = 422,
        Error = 500,
        Unauthorized = 403,
        // Özel hata kodları (460–499 arası kullanışlıdır)
        InvalidCustomerLocation = 460,  //Müşteri konumu geçersiz
        DistanceNotSatisfied = 461,// Belirtilen mesafe şartı sağlanmadı
    }
}

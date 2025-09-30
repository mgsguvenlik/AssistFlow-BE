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
        Unauthorized = 403
    }
}

using Mapster;
using Model.Concrete;
using Model.Dtos.User;

namespace Business.Mapper
{
    public static class MapsterConfig
    {
        public static void Configure()
        {
            TypeAdapterConfig<User, UserCreateDto>.NewConfig();
        }
    }
}

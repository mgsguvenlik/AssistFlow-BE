namespace Business.Interfaces
{
    public interface IMenuService
        : ICrudService<Model.Dtos.Menu.MenuCreateDto,
                       Model.Dtos.Menu.MenuUpdateDto,
                       Model.Dtos.Menu.MenuGetDto,
                       long>
    { }
}

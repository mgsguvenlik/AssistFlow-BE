using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Model.Dtos.MailOutbox;
using System.Linq.Expressions;

public class MailOutboxService
  : CrudServiceBase<Model.Concrete.MailOutbox, long, MailOutboxCreateDto, MailOutboxUpdateDto, MailOutboxGetDto>,
    IMailOutboxService
{
    public MailOutboxService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
        : base(uow, mapper, config) { }

    protected override long ReadKey(Model.Concrete.MailOutbox e) => e.Id;

    protected override Expression<Func<Model.Concrete.MailOutbox, bool>> KeyPredicate(long id)
        => m => m.Id == id;

    protected override async Task<Model.Concrete.MailOutbox?> ResolveEntityForUpdateAsync(MailOutboxUpdateDto dto)
    {
        if (dto.Id <= 0) return null;

        // tracked entity (update için tracking açık)
        var entity = await _unitOfWork.Repository.GetByIdAsync<Model.Concrete.MailOutbox>(
            asNoTracking: false,
            id: dto.Id
        );

        return entity;
    }

    // Basit manuel retry
    public async Task<bool> RetryAsync(long id, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.Repository.GetByIdAsync<Model.Concrete.MailOutbox>(
            asNoTracking: false, id: id);
        if (entity == null) return false;

        // >>> DÜZELTME: enum ile karşılaştır
        //if (entity.Status == MailOutboxStatus.Sent ||
        //    entity.Status == MailOutboxStatus.InProgress)
        //    return false;

        entity.Status = MailOutboxStatus.Pending;     // >>> DÜZELTME: enum atama
        entity.NextAttemptAt = DateTime.Now;
        entity.TryCount = Math.Max(0, entity.TryCount - 1); // istersen 0’a çek
        entity.LastError = null;

        _unitOfWork.Repository.Update(entity);
        await _unitOfWork.Repository.CompleteAsync();
        return true;
    }
}

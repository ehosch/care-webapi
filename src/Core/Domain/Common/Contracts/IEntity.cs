namespace Care.WebApi.Domain.Common.Contracts;

public interface IEntity<TId>
{
    TId Id { get; }
}

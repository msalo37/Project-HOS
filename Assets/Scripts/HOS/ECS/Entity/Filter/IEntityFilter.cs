namespace HOS.ECS.Entity
{
    public interface IEntityFilter
    {
        public bool TryPassFilter(BaseEntity baseEntity);
    }
}
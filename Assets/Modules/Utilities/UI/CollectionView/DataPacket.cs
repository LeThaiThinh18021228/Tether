using Framework.SimpleJSON;

namespace Utilities
{
    public interface IDataUnit<T>
    {
        public int Id { get; set; }
    }
    public interface IEntity<T>
    {
        public JSONNode ToJson(IEntity<T> obj);
        public IEntity<T> FromJson(JSONNode data);
    }
}


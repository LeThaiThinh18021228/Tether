using Framework;
using UnityEngine;

namespace Utilities
{
    public interface ICard<T> where T : IDataUnit<T>
    {
        void BuildView(T info);
    }
    public abstract class CardBase<T> : CacheMonoBehaviour, ICard<T> where T : IDataUnit<T>
    {
        protected T info; public T Info { get { return info; } }
        [HideInInspector] public CollectionViewBase<T> View;
        public virtual void BuildView(T info)
        {
            this.info = info;
        }
        public void BuildView(T info, CollectionViewBase<T> collectionView)
        {
            this.View = collectionView;
            BuildView(info);
        }
    }
}

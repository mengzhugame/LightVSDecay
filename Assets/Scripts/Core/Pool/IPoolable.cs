namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// 对象池物体接口
    /// 所有需要使用对象池的物体都必须实现此接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 从池中取出时调用（相当于 Awake/Start）
        /// </summary>
        void OnSpawn();
        
        /// <summary>
        /// 回收到池中时调用（相当于 OnDestroy）
        /// </summary>
        void OnDespawn();
        
        /// <summary>
        /// 获取该对象所属的池类型标识
        /// </summary>
        string PoolKey { get; }
    }
}
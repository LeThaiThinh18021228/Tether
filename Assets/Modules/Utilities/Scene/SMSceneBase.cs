using Framework;
using System;

namespace Utilities
{
    [Serializable]
    public abstract class SMSceneBase : StateMachineBase<SMSceneBase>
    {
        public ESceneName CurScene { get; protected set; } = ESceneName.Loading;
        public SMSceneBase(IState<SMSceneBase> state) : base(state)
        {

        }

        public abstract void ChangeState(ESceneName scene);
    }
}


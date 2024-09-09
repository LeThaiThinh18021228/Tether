using DG.Tweening;
using Framework;
using System;
using UnityEngine;

namespace Utilities
{
    [Serializable]
    public class SMSceneTransition : StateMachineBase<SMSceneTransition>
    {
        public AsyncOperation SceneAsync;
        public Tween Tween;
        public Action<ESceneName> OnLoaded;
        public PopupBehaviour LoadingPopup;

        public SMSceneTransition(IState<SMSceneTransition> state) : base(state)
        {
        }
    }
}

﻿using UnityEngine;

namespace Utilities
{
    public class ButtonOpenURL : ButtonBase
    {
        [SerializeField] string _strURL;

        protected override void Button_OnClicked()
        {
            base.Button_OnClicked();

            Application.OpenURL(_strURL);
        }
    }
}
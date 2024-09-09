using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using FishNet;
public class PlayerNameSetter : MonoBehaviour
{
    [SerializeField] TMP_InputField nameInputField;
    // Start is called before the first frame update
    void Awake()
    {
        nameInputField.onSubmit.AddListener(NameOnSubmit);
    }

    private void NameOnSubmit(string name)
    {
        Player.PlayerOwner.Data.Name.Value = name;
    }
}

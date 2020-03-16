using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAsyncHttp.Utilities
{
    public class DontDestroyOnLoad : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }

}

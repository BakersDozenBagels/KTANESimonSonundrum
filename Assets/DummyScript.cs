using UnityEngine;

public class DummyScript : MonoBehaviour
{
    void Start()
    {
        GetComponent<KMSelectable>().OnInteract = () => { GetComponent<KMBombModule>().HandlePass(); return false; };
    }
}

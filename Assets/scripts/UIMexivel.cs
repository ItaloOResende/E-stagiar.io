using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMexivel : MonoBehaviour
{
    public GameObject HDVirado, chaveDeFendaPonta, espaco1, espaco2, espaco3;
    public bool mexendo, LocalCorreto1, LocalCorreto2, LocalCorreto3;
    public Button botaoProximo;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (mexendo == true)
        {
            if (this.name.StartsWith("HD Movel")) 
            {
                gameObject.SetActive(false);
                HDVirado.SetActive(true);
            }
            this.transform.position = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);
        }
        //verificar se existe o ui e verificar a distancia deles
        if (espaco1 != null)
        {
            if (Vector2.Distance(chaveDeFendaPonta.transform.position, espaco1.transform.position) < 5)
            {
                LocalCorreto1 = true;
            }
        }
        if (espaco2 != null)
        {
            if (Vector2.Distance(chaveDeFendaPonta.transform.position, espaco2.transform.position) < 5)
            {
                LocalCorreto2 = true;
            }
        }
        if (espaco3 != null)
        {
            if (Vector2.Distance(chaveDeFendaPonta.transform.position, espaco3.transform.position) < 5)
            {
                LocalCorreto3 = true;
            }
        }
        if ((LocalCorreto1 == true) && (LocalCorreto2 == true) && (LocalCorreto3 == true))
        {
            botaoProximo.interactable = true;
        }
    }
    public void mexer()
    {
        mexendo = !mexendo;
    }
}
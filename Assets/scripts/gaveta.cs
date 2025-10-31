using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gaveta : MonoBehaviour
{
    public GameObject jogador, Mao, imagemInteragir, objetos, objCriado;
    public bool aberto;

    void Start()
    {
        aberto = false;
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, jogador.transform.position) >= 3)
        {
            imagemInteragir.SetActive(false);
        }
    }

    //Função que verifica se o mouse está no objeto e mostra as imagens se o jogador estiver perto dele
    void OnMouseOver()
    {
        if (Vector3.Distance(transform.position, jogador.transform.position) < 3)
        {
            imagemInteragir.SetActive(true);
        }
        else
        {
            imagemInteragir.SetActive(false);
        }
    }
    
    //Função que verifica se o mouse n está mais no objeto
    void OnMouseExit()
    {
        imagemInteragir.SetActive(false);
    }

    //Função que abre/fecha a gaveta
    void OnMouseDown()
    {
        if ((Vector3.Distance(transform.position, jogador.transform.position) <= 3) && (imagemInteragir.activeSelf == true) && (aberto == false))
        {
            objetos = Instantiate(objCriado, new Vector3(Mao.transform.position.x, Mao.transform.position.y, Mao.transform.position.z), Mao.transform.rotation);
            transform.Translate(0, -1, 0);
            aberto = true;
        }
        else if ((Vector3.Distance(transform.position, jogador.transform.position) <= 3) && (imagemInteragir.activeSelf == true) && (aberto == true))
        {
            transform.Translate(0, 1, 0);
            aberto = false;
        }     
    }
}
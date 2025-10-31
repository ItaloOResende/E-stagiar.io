using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class jogador : MonoBehaviour
{
    //Variaveis da movimentação do jogador
    CharacterController controller;
    Vector3 forward;
    Vector3 strafe;
    Vector3 vertical;
    float forwardSpeed = 6f;
    float strafeSpeed = 6f;
    float gravity;
    float jumpSpeed;
    float maxJumpHeight = 2f;
    float timeToMaxHeight = 0.5f;

    //Variaveis para interagir com objetos
    public GameObject Mao, imagemPegar, imagemConsertar;
    public GameObject[] ObjetosSeguraveis;
    public bool seguraObjeto, interacaoRealizada;
    public List<bool> seguraObjetoLista;
    public int qtd, qtdObjetos, /*qtdObjetosAnterior, qtdObjetosAgora,*/ x, y;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        gravity = (-2 * maxJumpHeight) / (timeToMaxHeight * timeToMaxHeight);
        jumpSpeed = (2 * maxJumpHeight) / timeToMaxHeight;

        ObjetosSeguraveis = GameObject.FindGameObjectsWithTag("ObjetosSeguraveis");
        qtdObjetos = ObjetosSeguraveis.Length;
        //qtdObjetosAnterior = ObjetosSeguraveis.Length;
        for (qtd = 0; qtd < ObjetosSeguraveis.Length; qtd++)
        {
            seguraObjetoLista.Add(false);
        }
    }

    void Update()
    {
        movimentacao();
        segurarObjeto();
        ExpawnObjeto();
        if (ObjetosSeguraveis.Length > 0)
        {
            for (qtd = 0; qtd < qtdObjetos; qtd++)
            {
                if (seguraObjetoLista[qtd] == true)
                {
                    ObjetosSeguraveis[qtd].transform.position = Mao.transform.position;
                }
            }
        }
    }

    //Função de movimentação do jogador
    void movimentacao()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        // force = input * speed * direction
        forward = forwardInput * forwardSpeed * transform.forward;
        strafe = strafeInput * strafeSpeed * transform.right;

        vertical += gravity * Time.deltaTime * Vector3.up;

        if (controller.isGrounded)
        {
            vertical = Vector3.down;
        }

        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
        {
            vertical = jumpSpeed * Vector3.up;
        }

        if (vertical.y > 0 && (controller.collisionFlags & CollisionFlags.Above) != 0)
        {
            vertical = Vector3.zero;
        }

        Vector3 finalVelocity = forward + strafe + vertical;

        controller.Move(finalVelocity * Time.deltaTime);
    }

    //Função que deixa a mãozinha visivel e invisivel e que permite segurar os objetos proximos ao personagem
    public void segurarObjeto()
    {
        int objetoInterativoIndex = -1; 
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Input.GetMouseButtonDown(0) && (seguraObjeto == true))
        {
            for (qtd = 0; qtd < ObjetosSeguraveis.Length; qtd++)
            {
                seguraObjetoLista[qtd] = false;
                seguraObjeto = false;
            }
        }
        if (Physics.Raycast(ray, out hit))
        {
            GameObject hitObject = hit.collider.gameObject;

            for (qtd = 0; qtd < ObjetosSeguraveis.Length; qtd++)
            {
                if (hitObject == ObjetosSeguraveis[qtd] && Vector3.Distance(transform.position, hitObject.transform.position) <= 3)
                {
                    objetoInterativoIndex = qtd; 
                    interacaoRealizada = true;
                    if (Input.GetMouseButtonDown(0) && (seguraObjeto == false) && (imagemPegar.activeSelf))
                    {
                        seguraObjetoLista[qtd] = true;
                        seguraObjeto = true;
                    }
                    break;
                }
                else
                {
                    interacaoRealizada = false;
                }
            }
        }
        if (interacaoRealizada)
        {
            imagemPegar.SetActive(true);
        }
        else
        {
            imagemPegar.SetActive(false);
        }
    }

    //Função que expawna objetos (ela é chamda na função OnMouseDown();)
    void ExpawnObjeto()
    {
        ObjetosSeguraveis = GameObject.FindGameObjectsWithTag("ObjetosSeguraveis");
        //qtdObjetosAgora = ObjetosSeguraveis.Length;
        if (seguraObjetoLista.Count != ObjetosSeguraveis.Length)
        {
            while (seguraObjetoLista.Count < ObjetosSeguraveis.Length)
            {
                seguraObjetoLista.Add(true);
                seguraObjeto = true;
                qtdObjetos++;
            }

            while (seguraObjetoLista.Count > ObjetosSeguraveis.Length)
            {
                seguraObjetoLista.RemoveAt(seguraObjetoLista.Count - 1);
                qtdObjetos--;
            }
        }
    }
}
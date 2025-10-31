using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class contadorDePontos : MonoBehaviour
{
    //Variaveis de tempo
    public float timeLimit;
    public float currentTime = 0f, tempoDeNotificacao;
    public bool timerRunning;

    public GameObject celular, rawNotificacao, falta, concluido;
    public TMP_Text textoPontos, TextoNotificacao, textoTempo, textoTarefa1, textoTarefa2, textoTarefa3;

    public interagePC[] objetosInteragePC;
    public List<int> consertarAlgo = new List<int>(), consertarPC = new List<int>();
    public int pcConsertado, redeConsertada, pontosTotal;
    public float intervaloDeAparecimento = 5f;
    public bool celularAtivado;

    void Start()
    {
        StartTimer();
    }

    void Update()
    {
        mostraTarefas();
        timerRun();
        apareceCelular();
        //Aqui conta os segundos para chamar a função carregaNovasTarefas()
        //Invoke("carregaNovasTarefas", 6f);
    }

    //Função que faz o celular aparecer na tela
    public void apareceCelular()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            celularAtivado = !celularAtivado;
            celular.SetActive(celularAtivado);
        }
    }

    //Função que faz a conta de pontos e de objetos consertados (é chamada pela função verificaTarefas())
    public void contaPontos()
    {
        if (consertarPC[0] > pcConsertado)
        {
            objetosInteragePC = FindObjectsOfType<interagePC>();
            pontosTotal = 0;
            pcConsertado = 0;

            foreach (var objetoInteragePC in objetosInteragePC)
            {
                pontosTotal += objetoInteragePC.pontos;
                pcConsertado += objetoInteragePC.objConsertado;
            }
            textoPontos.text = pontosTotal.ToString();
        }
    }

    //Função que mostra as tarefas
    public void mostraTarefas()
    {
        for (int qtd = 0; qtd < consertarAlgo.Count; qtd++)
        {
            for (int qtd2 = 0; qtd2 < consertarPC.Count; qtd2++)
            {
                if (consertarAlgo[qtd] == 1)
                {
                    if (consertarAlgo.Count > 0)
                    {
                        textoTarefa1.text = "CONSERTAR PCS: (" + pcConsertado + "/" + consertarPC[0] + ")";
                    }
                    else
                    {
                        textoTarefa1.text = "";
                    }
                }
                else
                {
                    if (consertarAlgo.Count > 1)
                    {
                        textoTarefa2.text = "ARRUME A REDE: (" + redeConsertada + "/" + consertarPC[1] + ")";
                    }
                    else
                    {
                        textoTarefa2.text = "";
                    }
                }
            }
        }
    }

    //Função que ativa a notificação da tarefa quando parte dela é feita (é chamada no script interagePC na função estadoPC())
    public void verificaTarefas()
    {
        contaPontos();
        if (consertarPC[0] >= pcConsertado + 1)
        {
            TextoNotificacao.text = "CONSERTAR PCS: (" + pcConsertado + "/" + consertarPC[0] + ")";
            rawNotificacao.SetActive(true);
            falta.SetActive(true);
            concluido.SetActive(false);
            tempoDeNotificacao = 3;
        }
        else
        {
            TextoNotificacao.text = "CONSERTAR PCS: (" + pcConsertado + "/" + consertarPC[0] + ")";
            rawNotificacao.SetActive(true);
            falta.SetActive(false);
            concluido.SetActive(true);
            tempoDeNotificacao = 3;
        }
    }
    
    //Função que inicia o timer
    public void StartTimer()
    {
        // Inicia o timer
        currentTime = timeLimit;
        timerRunning = true;
    }

    //Função que faz o timer correr
    public void timerRun()
    {
        //Trecho que atualiza o tempo restante e o exibe no texto em formato de minutos
        if (timerRunning)
        {
            if (currentTime < 1)
            {
                textoTempo.text = "FIM";
                timerRunning = false;
            }
            else
            {
                currentTime -= Time.deltaTime;
                int minutes = Mathf.FloorToInt(currentTime / 60f);
                int seconds = Mathf.FloorToInt(currentTime % 60f);
                textoTempo.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
        //Trecho que conta o tempo da notificação na tela
        if (tempoDeNotificacao < 1)
        {
            for (int qtd = 0; qtd < consertarAlgo.Count; qtd++)
            {
                for (int qtd2 = 0; qtd2 < consertarPC.Count; qtd2++)
                {
                    rawNotificacao.SetActive(false);
                }
            }
        }
        else
        {
            tempoDeNotificacao -= Time.deltaTime;
        }
    }
}
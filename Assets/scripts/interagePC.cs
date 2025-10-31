using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class interagePC : MonoBehaviour
{
    public contadorDePontos contador;
    public GameObject jogadorObj, cameraObj, imagemInteragir, canvasLigarPC, canvasAbrirPC, canvasMontarPC, monitorImgFrente, monitorImgAtras, monitorSemVideo, monitorNaBios, monitorNoSistema,
           caboDeEnergiaLigarPC, caboDeEnergiaLigarPC2, caboDeEnergiaImg, caboDeEnergiaImg2, caboDeEnergiaImg3, caboDeEnergiaImg4,  caboDeVideoImg, caboDeVideoImg2, caboDeRedeImg, caboDeRedeImg2,
           placaMaeImgFixa, placaMaeImgMovel, HDImgFixo, HDImgMovel,  HDVirado, memoriaRAMImgFixa, memoriaRAMImgMovel, botaoLigarGabinete, botaoLigarMonitor,
           espacoCaboDeEnergiaTomada1, espacoCaboDeEnergiaTomada2, espacoCaboDeEnergiaGabinete, espacoCaboDeEnergiaMonitor, espacoCaboDeVideoGabinete, espacoCaboDeVideoMonitor, 
           espacoCaboDeRedeCanaleta1, espacoCaboDeRedeCanaleta2, espacoCaboDeRedeGabinete,  
           espacoPlacaMae, espacoMemoriaRAM, espacoHD1, espacoHD2, espacoHD3;
    public GameObject[] objEncontrados, ObjetosSeguraveis;
    public List<GameObject> monitor, caboDeEnergia, caboDeVideo, caboDeRede, placaMae, HD, memoriaRAM;
    public int qtd, qtdTotal, objConsertado, pontos;
    public bool consertando, aberto, showCursor, marcouPonto, placaMaeFixa, memoriaRAMFixa, HDFixo, gabineteLigado, monitorLigado,
                caboDeEnergiaEncaixadoTomada, caboDeEnergiaEncaixadoTomada2, caboDeEnergiaEncaixadoGabinete, caboDeEnergiaEncaixadoMonitor, caboDeVideoEncaixadoGabinete, caboDeVideoEncaixadoMonitor, 
                caboDeRedeEncaixadoGabinete, caboDeRedeEncaixadoCanaleta, caboDeRedeEncaixadoCanaleta2,
                PlacaMaeEncaixada, RAMEncaixada, HDEncaixado;

    // Start is called before the first frame update
    void Start()
    {
        contador = FindObjectOfType<contadorDePontos>();
        jogadorObj = GameObject.FindGameObjectWithTag("Player");
        cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
        showCursor = false;
        placaMaeFixa = Random.Range(0, 2) == 0;
        if (placaMaeFixa == true)
        {
            memoriaRAMFixa = Random.Range(0, 2) == 0;
            HDFixo = Random.Range(0, 2) == 0;
        }
    }

    // Update is called once per frame
    void Update()
    {
        ObjetosSeguraveis = GameObject.FindGameObjectsWithTag("ObjetosSeguraveis");
        qtdTotal = ObjetosSeguraveis.Length;
        consertarObj();

        //Trecho para indentificar imagemInteragir que fica desabilitada ao começar o jogo
        GameObject[] objDesabilitado = Resources.FindObjectsOfTypeAll<GameObject>();
        indentificaObjetos();
        verificaPecas();
        estadoPC();
    }

    //Função que verifica se o mouse está no objeto e mostra as imagens se o jogador estiver perto dele
    void OnMouseOver()
    {
        for (qtd = 0; qtd < qtdTotal; qtd++)
        {
            if ((Vector3.Distance(transform.position, ObjetosSeguraveis[qtd].transform.position) < 3) && (consertando == false))
            {
                imagemInteragir.SetActive(true);
            }
        }
    }

    //Função que verifica se o mouse n está mais no objeto
    void OnMouseExit()
    {
        imagemInteragir.SetActive(false);
    }

    //Função q define a visibilidade do cursor do mouse
    private void SetCursorVisibility()
    {
        Cursor.visible = showCursor;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    //Função que diferencia os objetosSeguraveis pelo nome deles
    public void indentificaObjetos()
    {
        if (placaMaeFixa == false)
        {
            placaMaeImgFixa.SetActive(false);
        }
        if (memoriaRAMFixa == false)
        {
            memoriaRAMImgFixa.SetActive(false);
        }
        if (HDFixo == false)
        {
            HDImgFixo.SetActive(false);
        }
        //Trecho para indentificar os objetos tudo que estão no jogo
        objEncontrados = GameObject.FindObjectsOfType<GameObject>();
        //Trecho para indentificar os monitores que estão no jogo
        monitor = new List<GameObject>();
        foreach (GameObject monitores in objEncontrados)
        {
            if (monitores.name.StartsWith("monitor"))
            {
                monitor.Add(monitores);
            }
        }
        //Trecho para indentificar os cabos de energia que estão no jogo
        caboDeEnergia = new List<GameObject>();
        foreach (GameObject cabosDeEnergia in objEncontrados)
        {
            if (cabosDeEnergia.name.StartsWith("cabo de energia("))
            {
                caboDeEnergia.Add(cabosDeEnergia);
            }
        }
        //Trecho para indentificar os cabos de video que estão no jogo
        caboDeVideo = new List<GameObject>();
        foreach (GameObject cabosDeVideo in objEncontrados)
        {
            if (cabosDeVideo.name.StartsWith("cabo de video("))
            {
                caboDeVideo.Add(cabosDeVideo);
            }
        }
        //Trecho para indentificar os cabos de rede que estão no jogo
        caboDeRede = new List<GameObject>();
        foreach (GameObject cabosDeRede in objEncontrados)
        {
            if (cabosDeRede.name.StartsWith("cabo de rede("))
            {
                caboDeRede.Add(cabosDeRede);
            }
        }
        //Trecho para indentificar os HDS que estão no jogo
        HD = new List<GameObject>();
        foreach (GameObject HDs in objEncontrados)
        {
            if (HDs.name.StartsWith("HD("))
            {
                HD.Add(HDs);
            }
        }
        //Trecho para indentificar as memória RAM que estão no jogo
        memoriaRAM = new List<GameObject>();
        foreach (GameObject memoriaRAMS in objEncontrados)
        {
            if (memoriaRAMS.name.StartsWith("memoria RAM("))
            {
                memoriaRAM.Add(memoriaRAMS);
            }
        }
        //Trecho para indentificar as Placa mãe que estão no jogo
        placaMae = new List<GameObject>();
        foreach (GameObject placaMaes in objEncontrados)
        {
            if (placaMaes.name.StartsWith("placa mae("))
            {
                placaMae.Add(placaMaes);
            }
        }
    }

    //Função para desabilitar o movimento do jogador (é ativado com a função soltarObjeto)
    void DesabilitarMovimentos()
    {
        jogador jogadorScript = jogadorObj.GetComponent<jogador>();
        camera cameraScript = cameraObj.GetComponent<camera>();
        if (jogadorScript != null)
        {
            jogadorScript.enabled = false;
        }
        if (cameraScript != null)
        {
            cameraScript.enabled = false;
        }
        showCursor = !showCursor;
        SetCursorVisibility();
    }

    //Função para habilitar o movimento do jogador (é ativado com a função soltarObjeto)
    public void habilitarMovimento()
    {
        jogador jogadorScript = jogadorObj.GetComponent<jogador>();
        camera cameraScript = cameraObj.GetComponent<camera>();
        if (jogadorScript != null)
        {
            jogadorScript.enabled = true;
        }
        if (cameraScript != null)
        {
            cameraScript.enabled = true;
        }
        canvasLigarPC.SetActive(false);
        canvasMontarPC.SetActive(false);
        canvasAbrirPC.SetActive(false);
        showCursor = !showCursor;
        SetCursorVisibility();
    }

    //Função para mudar o canvas que aparece na tela (com direção pra frente)
    public void mudarCanvasAvancar()
    {
        if (canvasLigarPC.activeSelf)
        {
            canvasAbrirPC.SetActive(true);
            canvasLigarPC.SetActive(false);
        }
        else if (canvasAbrirPC.activeSelf)
        {
            canvasMontarPC.SetActive(true);
            canvasAbrirPC.SetActive(false);
        }
        else if (canvasMontarPC.activeSelf)
        {
            canvasLigarPC.SetActive(true);
            canvasMontarPC.SetActive(false);
        }
    }

    //Função para mudar o canvas que aparece na tela (com direção pra frente)
    public void mudarCanvasVoltar()
    {
        if (canvasAbrirPC.activeSelf)
        {
            canvasLigarPC.SetActive(true);
            canvasAbrirPC.SetActive(false);
        }
        else if (canvasMontarPC.activeSelf)
        {
            canvasAbrirPC.SetActive(true);
            canvasMontarPC.SetActive(false);
        }
    }

    //Função para consertar objeto (desabilita movimentos e ativa minigames)
    void consertarObj()
    {
        jogador jogadorScript = jogadorObj.GetComponent<jogador>();
        if (Input.GetMouseButtonDown(1))
        {
            if (consertando == false)
            {
                if ((Vector3.Distance(transform.position, jogadorObj.transform.position) <= 3) && (imagemInteragir.activeSelf) && (jogadorScript.enabled == true))
                {
                    consertando = true;
                    DesabilitarMovimentos();
                    imagemInteragir.SetActive(false);
                    canvasLigarPC.SetActive(true);
                    if (monitor.Count > 0)
                    {
                        for (qtd = 0; qtd < monitor.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, monitor[qtd].transform.position) <= 3)
                            {
                                monitorImgFrente.SetActive(true);
                                monitorImgAtras.SetActive(true);
                                break;
                            }
                            else
                            {
                                monitorImgFrente.SetActive(false);
                                monitorImgAtras.SetActive(false);
                            }
                        }
                    }
                    if (caboDeEnergia.Count > 0)
                    {
                        for (qtd = 0; qtd < caboDeEnergia.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, caboDeEnergia[qtd].transform.position) <= 3)
                            {
                                if (!caboDeEnergiaImg3.activeSelf)
                                {
                                    Destroy(caboDeEnergia[qtd]);
                                }
                                if (!caboDeEnergiaImg.activeSelf)
                                {
                                    caboDeEnergiaImg.SetActive(true);
                                    caboDeEnergiaImg2.SetActive(true);
                                }
                                else
                                {
                                    caboDeEnergiaImg3.SetActive(true);
                                    caboDeEnergiaImg4.SetActive(true);
                                }
                            }
                        }
                    }
                    if (caboDeVideo.Count > 0)
                    {
                        for (qtd = 0; qtd < caboDeVideo.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, caboDeVideo[qtd].transform.position) <= 3)
                            {
                                if (!caboDeVideoImg.activeSelf)
                                {
                                    Destroy(caboDeVideo[qtd]);
                                }
                                caboDeVideoImg.SetActive(true);
                                caboDeVideoImg2.SetActive(true);
                            }
                        }
                    }
                    if (caboDeRede.Count > 0)
                    {
                        for (qtd = 0; qtd < caboDeRede.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, caboDeRede[qtd].transform.position) <= 3)
                            {
                                if (!caboDeRedeImg.activeSelf)
                                {
                                    Destroy(caboDeRede[qtd]);
                                }
                                caboDeRedeImg.SetActive(true);
                                caboDeRedeImg2.SetActive(true);
                            }
                        }
                    }
                    if (HD.Count > 0)
                    {
                        for (qtd = 0; qtd < HD.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, HD[qtd].transform.position) <= 3)
                            {
                                if (!HDImgMovel.activeSelf)
                                {
                                    Destroy(HD[qtd]);
                                }
                                HDImgMovel.SetActive(true);
                            }
                        }
                    }
                    if (memoriaRAM.Count > 0)
                    {
                        for (qtd = 0; qtd < memoriaRAM.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, memoriaRAM[qtd].transform.position) <= 3)
                            {
                                if (!memoriaRAMImgMovel.activeSelf)
                                {
                                    Destroy(memoriaRAM[qtd]);
                                }
                                memoriaRAMImgMovel.SetActive(true);
                            }
                        }
                    }
                    if (placaMae.Count > 0)
                    {
                        for (qtd = 0; qtd < placaMae.Count; qtd++)
                        {
                            if (Vector3.Distance(transform.position, placaMae[qtd].transform.position) <= 3)
                            {
                                if (!placaMaeImgMovel.activeSelf)
                                {
                                    Destroy(placaMae[qtd]);
                                }
                                placaMaeImgMovel.SetActive(true);
                            }
                        }
                    }
                }
                consertando = false;
            }
        }
    }

    //Função que verifica se as peças estão no local correto
    void verificaPecas()
    {
        //Parte que verifica os objetos do canvasAbrirPC
        if ((caboDeEnergia != null) && (canvasAbrirPC.activeSelf))
        {
            //Parte que verifica se tem cabo de energia no gabinete
            if ((Vector2.Distance(caboDeEnergiaImg.transform.position, espacoCaboDeEnergiaGabinete.transform.position) < 5) || 
                (Vector2.Distance(caboDeEnergiaImg2.transform.position, espacoCaboDeEnergiaGabinete.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg3.transform.position, espacoCaboDeEnergiaGabinete.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg4.transform.position, espacoCaboDeEnergiaGabinete.transform.position) < 5))
            {
                caboDeEnergiaEncaixadoGabinete = true;
            }
            else
            {
                caboDeEnergiaEncaixadoGabinete = false;
            }
            //Parte que verifica se tem cabo de energia na tomada
            if ((Vector2.Distance(caboDeEnergiaImg.transform.position, espacoCaboDeEnergiaTomada1.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg2.transform.position, espacoCaboDeEnergiaTomada1.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg3.transform.position, espacoCaboDeEnergiaTomada1.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg4.transform.position, espacoCaboDeEnergiaTomada1.transform.position) < 5))
            {
                caboDeEnergiaEncaixadoTomada = true;
            }
            else
            {
                caboDeEnergiaEncaixadoTomada = false;
            }
            if ((Vector2.Distance(caboDeEnergiaImg.transform.position, espacoCaboDeEnergiaTomada2.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg2.transform.position, espacoCaboDeEnergiaTomada2.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg3.transform.position, espacoCaboDeEnergiaTomada2.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg4.transform.position, espacoCaboDeEnergiaTomada2.transform.position) < 5))
            {
                caboDeEnergiaEncaixadoTomada2 = true;
            }
            else
            {
                caboDeEnergiaEncaixadoTomada2 = false;
            }
            //Parte que verifica se tem cabo de energia no monitor
            if ((Vector2.Distance(caboDeEnergiaImg.transform.position, espacoCaboDeEnergiaMonitor.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg2.transform.position, espacoCaboDeEnergiaMonitor.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg3.transform.position, espacoCaboDeEnergiaMonitor.transform.position) < 5) ||
                (Vector2.Distance(caboDeEnergiaImg4.transform.position, espacoCaboDeEnergiaMonitor.transform.position) < 5))
            {
                caboDeEnergiaEncaixadoMonitor = true;
            }
            else
            {
                caboDeEnergiaEncaixadoMonitor = false;
            }
        }
        if ((caboDeVideo != null) && (canvasAbrirPC.activeSelf))
        {
            //Parte que verifica se tem cabo de video no gabinete
            if ((Vector2.Distance(caboDeVideoImg.transform.position, espacoCaboDeVideoGabinete.transform.position) < 5) ||
                (Vector2.Distance(caboDeVideoImg2.transform.position, espacoCaboDeVideoGabinete.transform.position) < 5))
            {
                caboDeVideoEncaixadoGabinete = true;
            }
            else
            {
                caboDeVideoEncaixadoGabinete = false;
            }
            //Parte que verifica se tem cabo de video no monitor
            if ((Vector2.Distance(caboDeVideoImg.transform.position, espacoCaboDeVideoMonitor.transform.position) < 5) ||
                (Vector2.Distance(caboDeVideoImg2.transform.position, espacoCaboDeVideoMonitor.transform.position) < 5))
            {
                caboDeVideoEncaixadoMonitor = true;
            }
            else
            {
                caboDeVideoEncaixadoMonitor = false;
            }
        }
        if ((caboDeRede != null) && (canvasAbrirPC.activeSelf))
        {
            //Parte que verifica se tem cabo de rede no gabinete
            if ((Vector2.Distance(caboDeRedeImg.transform.position, espacoCaboDeRedeGabinete.transform.position) < 5) ||
                (Vector2.Distance(caboDeRedeImg2.transform.position, espacoCaboDeRedeGabinete.transform.position) < 5))
            {
                caboDeRedeEncaixadoGabinete = true;
            }
            else
            {
                caboDeRedeEncaixadoGabinete = false;
            }
            //Parte que verifica se tem cabo de rede na caneleta
            if ((Vector2.Distance(caboDeRedeImg.transform.position, espacoCaboDeRedeGabinete.transform.position) < 5) ||
                (Vector2.Distance(caboDeRedeImg2.transform.position, espacoCaboDeRedeGabinete.transform.position) < 5))
            {
                caboDeRedeEncaixadoCanaleta = true;
            }
            else
            {
                caboDeRedeEncaixadoCanaleta = false;
            }
            if ((Vector2.Distance(caboDeRedeImg.transform.position, espacoCaboDeRedeCanaleta1.transform.position) < 5) ||
                (Vector2.Distance(caboDeRedeImg2.transform.position, espacoCaboDeRedeCanaleta2.transform.position) < 5))
            {
                caboDeRedeEncaixadoCanaleta2 = true;
            }
            else
            {
                caboDeRedeEncaixadoCanaleta2 = false;
            }
        }
        //Parte que verifica os objetos do canvasMontarPC
        if ((espacoPlacaMae != null) && (canvasMontarPC.activeSelf))
        {
            if ((Vector2.Distance(placaMaeImgMovel.transform.position, espacoPlacaMae.transform.position) < 5) ||
                ((placaMaeFixa == true) && (Vector2.Distance(placaMaeImgFixa.transform.position, espacoPlacaMae.transform.position) < 5)))
            {
                PlacaMaeEncaixada = true;
            }
            else
            {
                PlacaMaeEncaixada = false;
            }
        }
        if ((espacoMemoriaRAM != null) && (canvasMontarPC.activeSelf))
        {
            if ((Vector2.Distance(memoriaRAMImgMovel.transform.position, espacoMemoriaRAM.transform.position) < 5) ||
                ((memoriaRAMFixa == true) && (Vector2.Distance(memoriaRAMImgFixa.transform.position, espacoMemoriaRAM.transform.position) < 5)))
            {
                RAMEncaixada = true;
            }
            else
            {
                RAMEncaixada = false;
            }
        }
        if ((espacoHD1 != null) && (canvasMontarPC.activeSelf))
        {
            if ((Vector2.Distance(HDVirado.transform.position, espacoHD1.transform.position) < 5) ||
                (Vector2.Distance(HDVirado.transform.position, espacoHD2.transform.position) < 5) ||
                (Vector2.Distance(HDVirado.transform.position, espacoHD3.transform.position) < 5) ||
                ((HDFixo == true) && (Vector2.Distance(HDImgFixo.transform.position, espacoHD1.transform.position) < 5)) ||
                ((HDFixo == true) && (Vector2.Distance(HDImgFixo.transform.position, espacoHD2.transform.position) < 5)) ||
                ((HDFixo == true) && (Vector2.Distance(HDImgFixo.transform.position, espacoHD3.transform.position) < 5)))
            {
                HDEncaixado = true;
            }
            else
            {
                HDEncaixado = false;
            }
        }
    }

    //Função que verifica o estado do PC e qual tela deve aparecer no monitor no momento
    void estadoPC()
    {
        if (caboDeEnergiaEncaixadoTomada == true)
        {
            caboDeEnergiaLigarPC.SetActive(true);
        }
        else
        {
            caboDeEnergiaLigarPC.SetActive(false);
        }
        if (caboDeEnergiaEncaixadoTomada2 == true)
        {
            caboDeEnergiaLigarPC2.SetActive(true);
        }
        else
        {
            caboDeEnergiaLigarPC2.SetActive(false);
        }
        if (monitorLigado == true)
        {
            if ((caboDeEnergiaEncaixadoMonitor == true) && (caboDeEnergiaEncaixadoTomada == true) || (caboDeEnergiaEncaixadoTomada2 == true))
            {
                if ((gabineteLigado == true) && (caboDeVideoEncaixadoMonitor == true) && (caboDeEnergiaEncaixadoGabinete == true) && (caboDeVideoEncaixadoGabinete == true) && (PlacaMaeEncaixada == true))
                {
                    if ((RAMEncaixada == true) && (HDEncaixado == true))
                    {
                        monitorSemVideo.SetActive(false);
                        monitorNaBios.SetActive(false);
                        monitorNoSistema.SetActive(true);
                        if (marcouPonto == false)
                        {
                            pontos = pontos + 10;
                            objConsertado = objConsertado + 1;
                            marcouPonto = true;
                            contador.verificaTarefas();
                        }
                    }
                    else
                    {
                        monitorSemVideo.SetActive(false);
                        monitorNaBios.SetActive(true);
                        monitorNoSistema.SetActive(false);
                    }
                }
                else
                {
                    monitorSemVideo.SetActive(true);
                    monitorNaBios.SetActive(false);
                    monitorNoSistema.SetActive(false);
                }
            }
            else
            {
                monitorSemVideo.SetActive(false);
                monitorNaBios.SetActive(false);
                monitorNoSistema.SetActive(false);
            }
        }
        else
        {
            monitorSemVideo.SetActive(false);
            monitorNaBios.SetActive(false);
            monitorNoSistema.SetActive(false);
        }
    }

    //Função para ligar o gabinete com o "botao Ligar" do gabinete
    public void ligarGabinete()
    {
        gabineteLigado = !gabineteLigado;

    }

    //Função para ligar o monitor com o "botao Ligar" do monitor
    public void ligarMonitor()
    {
        monitorLigado = !monitorLigado;
    }
}
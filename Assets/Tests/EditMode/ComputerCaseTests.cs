using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ComputerCaseTests
{
    private GameObject go;
    private ComputerCase pcCase;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("Gabinete_Teste");
        pcCase = go.AddComponent<ComputerCase>();
        pcCase.Configure(ComputerCase.CreateDefaultTowerBackScrews());
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    private void RemoveAll(ToolType tool = ToolType.Screwdriver)
    {
        for (int i = 0; i < pcCase.TotalCount; i++)
            Assert.AreEqual(ScrewRemovalResult.Removed, pcCase.TryRemoveScrew(i, tool));
    }

    // ---- modelo: parafusos reais -----------------------------------------------------------------------------

    [Test]
    public void DefaultTowerBack_HasEightUniqueScrewsInsideImage()
    {
        Assert.AreEqual(8, pcCase.TotalCount);
        var ids = new HashSet<string>();
        foreach (ScrewData s in pcCase.Screws)
        {
            Assert.IsTrue(ids.Add(s.id), "id repetido: " + s.id);
            Assert.That(s.normalizedPosition.x, Is.InRange(0f, 1f));
            Assert.That(s.normalizedPosition.y, Is.InRange(0f, 1f));
            Assert.AreEqual(ToolType.Screwdriver, s.requiredTool);
            Assert.IsFalse(s.removed);
        }
    }

    [Test]
    public void DefaultTowerBack_MatchesMeasuredPixelCenters()
    {
        // Centros medidos em pc_back (710x1580): primeiro e último parafuso.
        ScrewData first = pcCase.Screws[0];
        ScrewData last = pcCase.Screws[7];
        Assert.AreEqual(118.4f / 710f, first.normalizedPosition.x, 1e-4f);
        Assert.AreEqual(33.2f / 1580f, first.normalizedPosition.y, 1e-4f);
        Assert.AreEqual(679.8f / 710f, last.normalizedPosition.x, 1e-4f);
        Assert.AreEqual(1456.8f / 1580f, last.normalizedPosition.y, 1e-4f);
    }

    // ---- remoção e ferramenta ------------------------------------------------------------------------------

    [Test]
    public void NoTool_CannotRemoveScrew()
    {
        Assert.AreEqual(ScrewRemovalResult.WrongTool, pcCase.TryRemoveScrew(0, ToolType.None));
        Assert.AreEqual(0, pcCase.RemovedCount);
    }

    [Test]
    public void WrongTool_CannotRemoveScrew()
    {
        Assert.AreEqual(ScrewRemovalResult.WrongTool, pcCase.TryRemoveScrew(3, ToolType.Other));
        Assert.IsFalse(pcCase.Screws[3].removed);
    }

    [Test]
    public void Screwdriver_RemovesScrewAndRaisesEventOnce()
    {
        var raised = new List<int>();
        pcCase.ScrewRemoved += raised.Add;

        Assert.AreEqual(ScrewRemovalResult.Removed, pcCase.TryRemoveScrew(2, ToolType.Screwdriver));
        Assert.AreEqual(ScrewRemovalResult.AlreadyRemoved, pcCase.TryRemoveScrew(2, ToolType.Screwdriver));

        Assert.AreEqual(1, pcCase.RemovedCount);
        Assert.AreEqual(new[] { 2 }, raised.ToArray());
    }

    [Test]
    public void InvalidIndex_IsRejected()
    {
        Assert.AreEqual(ScrewRemovalResult.InvalidScrew, pcCase.TryRemoveScrew(-1, ToolType.Screwdriver));
        Assert.AreEqual(ScrewRemovalResult.InvalidScrew, pcCase.TryRemoveScrew(8, ToolType.Screwdriver));
    }

    [Test]
    public void ScrewsAreIndependent_AnyOrderWorks()
    {
        pcCase.TryRemoveScrew(7, ToolType.Screwdriver);
        pcCase.TryRemoveScrew(0, ToolType.Screwdriver);
        Assert.AreEqual(2, pcCase.RemovedCount);
        Assert.IsTrue(pcCase.Screws[7].removed);
        Assert.IsTrue(pcCase.Screws[0].removed);
        Assert.IsFalse(pcCase.Screws[1].removed);
    }

    // ---- abertura ------------------------------------------------------------------------------------------

    [Test]
    public void CannotOpen_UntilEveryScrewIsRemoved()
    {
        for (int i = 0; i < 7; i++) pcCase.TryRemoveScrew(i, ToolType.Screwdriver);

        Assert.IsFalse(pcCase.AllScrewsRemoved);
        Assert.IsFalse(pcCase.CanOpen);
        Assert.AreEqual(CaseOpenResult.ScrewsRemaining, pcCase.TryOpen());
        Assert.AreEqual(CaseState.Closed, pcCase.State);
    }

    [Test]
    public void Opens_WhenAllScrewsRemoved_AndRaisesStateChangedOnce()
    {
        var states = new List<CaseState>();
        pcCase.StateChanged += states.Add;

        RemoveAll();

        Assert.IsTrue(pcCase.AllScrewsRemoved);
        Assert.IsTrue(pcCase.CanOpen);
        Assert.AreEqual(CaseOpenResult.Opened, pcCase.TryOpen());
        Assert.AreEqual(CaseOpenResult.AlreadyOpen, pcCase.TryOpen());
        Assert.AreEqual(CaseState.Open, pcCase.State);
        Assert.AreEqual(new[] { CaseState.Open }, states.ToArray());
        Assert.IsFalse(pcCase.CanOpen);
    }

    [Test]
    public void AfterOpen_ScrewsNoLongerChange()
    {
        RemoveAll();
        pcCase.TryOpen();
        Assert.AreEqual(ScrewRemovalResult.CaseAlreadyOpen, pcCase.TryRemoveScrew(0, ToolType.Screwdriver));
    }

    // ---- persistência / instâncias -------------------------------------------------------------------------

    [Test]
    public void State_PersistsOnTheLogicalObject_NotOnAnyScreen()
    {
        // A tela 2D só lê o ComputerCase: fechar/reabrir a tela = reler o mesmo objeto, sem perder o estado.
        pcCase.TryRemoveScrew(1, ToolType.Screwdriver);
        pcCase.TryRemoveScrew(4, ToolType.Screwdriver);

        ComputerCase sameObject = go.GetComponent<ComputerCase>();
        Assert.AreEqual(2, sameObject.RemovedCount);
        Assert.IsTrue(sameObject.Screws[1].removed);
        Assert.IsTrue(sameObject.Screws[4].removed);
    }

    [Test]
    public void TwoCases_AreIndependent()
    {
        var other = new GameObject("Outro").AddComponent<ComputerCase>();
        other.Configure(ComputerCase.CreateDefaultTowerBackScrews());
        try
        {
            pcCase.TryRemoveScrew(0, ToolType.Screwdriver);
            Assert.AreEqual(1, pcCase.RemovedCount);
            Assert.AreEqual(0, other.RemovedCount);
        }
        finally { Object.DestroyImmediate(other.gameObject); }
    }

    [Test]
    public void CustomScrewList_ComesFromData_NotFromCode()
    {
        pcCase.Configure(new[]
        {
            new ScrewData("a", 0.1f, 0.1f),
            new ScrewData("b", 0.9f, 0.9f),
            new ScrewData("c", 0.5f, 0.5f),
            new ScrewData("d", 0.5f, 0.9f),
            new ScrewData("e", 0.5f, 0.1f),
            new ScrewData("f", 0.2f, 0.8f),
        });
        Assert.AreEqual(6, pcCase.TotalCount);
    }

    [Test]
    public void CaseWithoutScrews_OpensDirectly()
    {
        pcCase.Configure(new List<ScrewData>());
        Assert.IsTrue(pcCase.CanOpen);
        Assert.AreEqual(CaseOpenResult.Opened, pcCase.TryOpen());
    }

    // ---- ToolItem ------------------------------------------------------------------------------------------

    [Test]
    public void ToolItem_ExposesTypeByComponent_NotByName()
    {
        var toolGo = new GameObject("QualquerNome");
        try
        {
            var tool = toolGo.AddComponent<ToolItem>();
            Assert.AreEqual(ToolType.Screwdriver, tool.Type);
            Assert.IsNotEmpty(tool.DisplayName);
        }
        finally { Object.DestroyImmediate(toolGo); }
    }
}

using LanceServer.Core.Symbol;
using LspTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Range = LspTypes.Range;

namespace LanceServerTest.Core.Symbol;

[TestClass]
public class ProcedureSymbolTest
{
    [TestMethod]
    public void ValueParametersMayBeOmittedPositionallyOrAtTheEnd()
    {
        var procedure = CreateProcedure(
            CreateParameter("depth", DataType.Real),
            CreateParameter("mode", DataType.Int),
            CreateParameter("enabled", DataType.Bool));

        Assert.IsTrue(procedure.ArgumentsMatchParameters(new[]
        {
            new ProcedureUseArgument(0),
            new ProcedureUseArgument(1, true),
            new ProcedureUseArgument(2)
        }));
        Assert.IsTrue(procedure.ArgumentsMatchParameters(new[]
        {
            new ProcedureUseArgument(0)
        }));
        Assert.IsTrue(procedure.ArgumentsMatchParameters(Array.Empty<ProcedureUseArgument>()));
    }

    [TestMethod]
    public void ReferenceParameterCannotBeOmitted()
    {
        var procedure = CreateProcedure(
            CreateParameter("depth", DataType.Real),
            CreateParameter("result", DataType.Int, isReferenceValue: true));

        Assert.IsFalse(procedure.ArgumentsMatchParameters(new[]
        {
            new ProcedureUseArgument(0),
            new ProcedureUseArgument(1, true)
        }));
        Assert.IsFalse(procedure.ArgumentsMatchParameters(new[]
        {
            new ProcedureUseArgument(0)
        }));
    }

    [TestMethod]
    public void AxisParameterCannotBeOmitted()
    {
        var procedure = CreateProcedure(CreateParameter("drillAxis", DataType.Axis));

        Assert.IsFalse(procedure.ArgumentsMatchParameters(Array.Empty<ProcedureUseArgument>()));
        Assert.IsFalse(procedure.ArgumentsMatchParameters(new[] { new ProcedureUseArgument(0, true) }));
        Assert.IsTrue(procedure.ArgumentsMatchParameters(new[] { new ProcedureUseArgument(0) }));
    }

    [TestMethod]
    public void ExternDeclarationMustContainCompleteInterface()
    {
        var procedure = CreateProcedure(
            CreateParameter("depth", DataType.Real),
            CreateParameter("mode", DataType.Int));

        Assert.IsFalse(procedure.DeclarationMatchesParameters(new[] { new ProcedureUseArgument(0) }));
        Assert.IsTrue(procedure.DeclarationMatchesParameters(new[]
        {
            new ProcedureUseArgument(0),
            new ProcedureUseArgument(1)
        }));
    }

    private static ProcedureSymbol CreateProcedure(params ParameterSymbol[] parameters)
    {
        var range = CreateRange();
        return new ProcedureSymbol(
            "OEM_CYCLE",
            new Uri("file:///CMA.DIR/OEM_CYCLE.SPF"),
            range,
            range,
            parameters);
    }

    private static ParameterSymbol CreateParameter(
        string identifier,
        DataType dataType,
        bool isReferenceValue = false)
    {
        var range = CreateRange();
        return new ParameterSymbol(
            identifier,
            new Uri("file:///CMA.DIR/OEM_CYCLE.SPF"),
            range,
            range,
            new CompositeDataType(dataType),
            Array.Empty<string>(),
            isReferenceValue);
    }

    private static Range CreateRange()
    {
        return new Range
        {
            Start = new Position(0, 0),
            End = new Position(0, 0)
        };
    }
}

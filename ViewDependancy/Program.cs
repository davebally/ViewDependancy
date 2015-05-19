using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.IO;


//using Microsoft.VisualStudio.Progression.Common;
using Microsoft.VisualStudio.GraphModel;
using System.Windows.Media;

namespace ViewDependancy
{
    class VwGraph
    {


        Graph graph = new Graph();
        String CurrentObject = "";
        String CurrentType = "";


        String databaseName = "";
        String schemaName = "";

        public SolidColorBrush getBrushForType(string Type){ 
            switch (Type)
            {
                case "View":
                    return(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x80, 0x80)));
                case "Proc":
                    return (new SolidColorBrush(Color.FromArgb(0xFF, 12, 231, 242)));
                case "Table":
                    return (new SolidColorBrush(Color.FromArgb(0xFF, 38, 242, 12)));

            } 
            return null;
        }

        public void addNode(String Node, String Type)
        {
            GraphPropertyCollection properties = graph.DocumentSchema.Properties;
            GraphProperty background = properties.AddNewProperty("Background", typeof(Brush));


            GraphNode nodeSource = graph.Nodes.GetOrCreate(Node);
            nodeSource.Label = Node;

            nodeSource[background] = getBrushForType(Type);

        }
        public void addNodesAndLink(String Source, String Target)
        {

            if (CurrentType == "") return;
          
            GraphNode nodeSource = graph.Nodes.GetOrCreate(Source);
            nodeSource.Label = Source;

            GraphNode nodeTarget = graph.Nodes.GetOrCreate(Target);
            nodeTarget.Label = Target;

            graph.Links.GetOrCreate(nodeSource, nodeTarget);

        }
        public void addSourcetoCurrentObject(String Source)
        {
            addNodesAndLink(Source, CurrentObject);
        }
        public void addTargettoCurrentObject(String Target)
        {
            addNodesAndLink(CurrentObject, Target);
        }

        public string GetFragmentType(TSqlFragment Statement)
        {
            String Type = Statement.ToString();
            String[] TypeSplit = Type.Split('.');
            String StmtType = TypeSplit[TypeSplit.Length - 1];
            return (StmtType);
        }

        public void ProcessSelectElement(IList<SelectElement> SelectElements)
        {
            foreach (SelectElement SelectElement in SelectElements)
            {
                //       ProcessSelectElement(SelectElement, ParentType, Cte);
            }

        }

        private void ProcessTableReference(TableReference TableRef)
        {
            string Type = GetFragmentType(TableRef);
            switch (Type)
            {
                case "FullTextTableReference":
                    break;
                case "NamedTableReference":
                    var NamedTableRef = (NamedTableReference)TableRef;
                    var Naming = NamedTableRef.SchemaObject;
                    string ObjectName = (Naming.DatabaseIdentifier == null ? this.databaseName : Naming.DatabaseIdentifier.Value) + "." +
                        (Naming.SchemaIdentifier == null ? this.schemaName : Naming.SchemaIdentifier.Value) + "." +
                        (Naming.BaseIdentifier == null ? "" : Naming.BaseIdentifier.Value);
                    addSourcetoCurrentObject(ObjectName);

                    break;
                case "QueryDerivedTable":
                    QueryDerivedTable qdt = (QueryDerivedTable)TableRef;
                    ProcessQueryExpression(qdt.QueryExpression);
                    break;
                case "QualifiedJoin":
                    QualifiedJoin qj = (QualifiedJoin)TableRef;
                    ProcessTableReference(qj.FirstTableReference);
                    ProcessTableReference(qj.SecondTableReference);
                    break;
                case "UnqualifiedJoin":
                    UnqualifiedJoin uqj = (UnqualifiedJoin)TableRef;
                    ProcessTableReference(uqj.FirstTableReference);
                    ProcessTableReference(uqj.SecondTableReference);
                    break;
                case "SchemaObjectFunctionTableReference":
                    SchemaObjectFunctionTableReference ftr = (SchemaObjectFunctionTableReference)TableRef;
                    break;
                case "PivotedTableReference":
                    PivotedTableReference pvt = (PivotedTableReference)TableRef;
                    ProcessTableReference(pvt.TableReference);
                    break;
                default:
                    break;
            }
        }

        public void ProcessFromClause(FromClause fromClause)
        {
            if (fromClause == null) return;
            foreach (var tableRef in fromClause.TableReferences)
            {
                ProcessTableReference(tableRef);
            }

        }
        public void ProcessScalarExpression(ScalarExpression se)
        {

            string ExpressionType = GetFragmentType(se);
            String ParameterType;
            switch (ExpressionType)
            {
                case "ConvertCall":
                    var ConvertCall = (ConvertCall)se;
                    ParameterType = GetFragmentType(ConvertCall.Parameter);
                    break;
                case "CastCall":
                    var CastCall = (CastCall)se;
                    ParameterType = GetFragmentType(CastCall.Parameter);
                    break;
                case "ScalarSubquery":
                    var SubQuery = (ScalarSubquery)se;
                    ProcessQueryExpression(SubQuery.QueryExpression);
                    break;
            }
        }

        public void ProcessBooleanExpression(BooleanExpression be)
        {
            String beType = GetFragmentType(be);
            switch (beType)
            {
                case "BooleanComparisonExpression":
                    var BoolComp = (BooleanComparisonExpression)be;
                    ProcessScalarExpression(BoolComp.FirstExpression);
                    ProcessScalarExpression(BoolComp.SecondExpression);

                    break;
                case "BooleanBinaryExpression":
                    var BoolExpression = (BooleanBinaryExpression)be;
                    ProcessBooleanExpression(BoolExpression.FirstExpression);
                    ProcessBooleanExpression(BoolExpression.SecondExpression);
                    break;
                default:
                    break;


            }



        }

        public void ProcessWhereClause(WhereClause whereClause)
        {
            if (whereClause == null) return;
            if (whereClause.SearchCondition != null) ProcessBooleanExpression(whereClause.SearchCondition);

        }
        public void ProcessQueryExpression(QueryExpression qe)
        {

            String qeType = GetFragmentType(qe);
            switch (qeType)
            {
                case "QuerySpecification":
                    var querySpec = (QuerySpecification)qe;
                    ProcessSelectElement(querySpec.SelectElements);
                    ProcessFromClause(querySpec.FromClause);
                    ProcessWhereClause(querySpec.WhereClause);
                    break;
                case "QueryParenthesisExpression":
                    var expression = (QueryParenthesisExpression)qe;
                    ProcessQueryExpression(expression.QueryExpression);

                    break;
                case "BinaryQueryExpression":
                    var binaryQueryExpression = (BinaryQueryExpression)qe;
                    ProcessQueryExpression(binaryQueryExpression.FirstQueryExpression);
                    ProcessQueryExpression(binaryQueryExpression.SecondQueryExpression);

                    break;

            }
        }

        public void ProcessWithCtesAndXmlNamespaces(WithCtesAndXmlNamespaces ctes)
        {
            foreach (var cte in ctes.CommonTableExpressions)
            {
                ProcessQueryExpression(cte.QueryExpression);

            }

        }
        public void ProcessSelectStatement(SelectStatement selStmt)
        {
            if (selStmt.WithCtesAndXmlNamespaces != null)
            {
                ProcessWithCtesAndXmlNamespaces(selStmt.WithCtesAndXmlNamespaces);
            }
            ProcessQueryExpression(selStmt.QueryExpression);


        }

        public void ProcessCreateProcedure(CreateProcedureStatement prcStmt)
        {
            foreach (var stmt in prcStmt.StatementList.Statements)
            {
                ProcessTsqlFragment(stmt);
            }

        }

       void  ProcessBeginEndBlockStatement(BeginEndBlockStatement  BEBlock)
        {
           foreach(var stmt in BEBlock.StatementList.Statements)
           {
               ProcessTsqlFragment(stmt);
           }
          

        }

        public void ProcessTryCatchStatement(TryCatchStatement fragment){
            foreach (var stmt in fragment.TryStatements.Statements)
            {
                ProcessTsqlFragment(stmt);
            }
            foreach(var stmt in fragment.CatchStatements.Statements)
            {
                ProcessTsqlFragment(stmt);
            }

        }

        public void ProcessUpdateStatement(UpdateStatement updStmt)
        {
            String TargetType = GetFragmentType(updStmt.UpdateSpecification.Target);
            switch(TargetType){
                case "NamedTableReference":
                    var NTR =(NamedTableReference)updStmt.UpdateSpecification.Target;
                    var TargetObject = (NTR.SchemaObject.DatabaseIdentifier == null ? this.databaseName : NTR.SchemaObject.DatabaseIdentifier.Value) + "." +
                        (NTR.SchemaObject.SchemaIdentifier == null ? this.schemaName : NTR.SchemaObject.SchemaIdentifier.Value) + "." +
                        (NTR.SchemaObject.BaseIdentifier == null ? "" : NTR.SchemaObject.BaseIdentifier.Value);
                    addTargettoCurrentObject(TargetObject);
                    break;



            }
     
        }

        public void  ProcessInsertStatement(InsertStatement insStmt)
        {
            String TargetType = GetFragmentType(insStmt.InsertSpecification.Target);
            switch(TargetType){
                case "NamedTableReference":
                    var NTR =(NamedTableReference)insStmt.InsertSpecification.Target;
                    var TargetObject = (NTR.SchemaObject.DatabaseIdentifier == null ? this.databaseName : NTR.SchemaObject.DatabaseIdentifier.Value) + "." +
                        (NTR.SchemaObject.SchemaIdentifier == null ? this.schemaName : NTR.SchemaObject.SchemaIdentifier.Value) + "." +
                        (NTR.SchemaObject.BaseIdentifier == null ? "" : NTR.SchemaObject.BaseIdentifier.Value);
                    addTargettoCurrentObject(TargetObject);
                    break;
            }
            if(insStmt.WithCtesAndXmlNamespaces!=null)ProcessWithCtesAndXmlNamespaces(insStmt.WithCtesAndXmlNamespaces);
            String insType = GetFragmentType(insStmt.InsertSpecification.InsertSource);
            switch(insType){
                default:
                    break;
            }
           }

        public void ProcessIfStatement(IfStatement ifStmt)
        {
            if (ifStmt.ThenStatement != null) ProcessTsqlFragment(ifStmt.ThenStatement);
            if (ifStmt.ElseStatement != null) ProcessTsqlFragment(ifStmt.ElseStatement);

        }
                    
        public void ProcessTsqlFragment(TSqlFragment fragment)
        {
            String stmtType = GetFragmentType(fragment);
            //Console.WriteLine(StmtType);
            switch (stmtType)
            {
                case "CreateTableStatement":
                    CreateTableStatement tblStmt = (CreateTableStatement)fragment;
                    CurrentType = "Table";
                    CurrentObject = (tblStmt.SchemaObjectName.DatabaseIdentifier == null ? this.databaseName : tblStmt.SchemaObjectName.DatabaseIdentifier.Value) + "." +
                        (tblStmt.SchemaObjectName.SchemaIdentifier == null ? this.schemaName : tblStmt.SchemaObjectName.SchemaIdentifier.Value) + "." +
                        (tblStmt.SchemaObjectName.BaseIdentifier == null ? "" : tblStmt.SchemaObjectName.BaseIdentifier.Value);
                    addNode(CurrentObject, "Table");
                    CurrentType = "";
                    break;
                case "CreateViewStatement":

                    CreateViewStatement vw = (CreateViewStatement)fragment;
                    CurrentType = "View";
                    CurrentObject = (vw.SchemaObjectName.DatabaseIdentifier == null ? this.databaseName : vw.SchemaObjectName.DatabaseIdentifier.Value) + "." +
                        (vw.SchemaObjectName.SchemaIdentifier == null ? this.schemaName : vw.SchemaObjectName.SchemaIdentifier.Value) + "." +
                        (vw.SchemaObjectName.BaseIdentifier == null ? "" : vw.SchemaObjectName.BaseIdentifier.Value);
                    addNode(CurrentObject, "View");
        
       
                    ProcessSelectStatement(vw.SelectStatement);
                    CurrentType = "";


                    break;
                case "CreateProcedureStatement":
                    CreateProcedureStatement prc = (CreateProcedureStatement)fragment;
                     CurrentType = "Proc";

                     CurrentObject = (prc.ProcedureReference.Name.DatabaseIdentifier == null ? this.databaseName : prc.ProcedureReference.Name.DatabaseIdentifier.Value) + "." +
                        (prc.ProcedureReference.Name.SchemaIdentifier == null ? this.schemaName : prc.ProcedureReference.Name.SchemaIdentifier.Value) + "." +
                        (prc.ProcedureReference.Name.BaseIdentifier == null ? "" : prc.ProcedureReference.Name.BaseIdentifier.Value);
                     addNode(CurrentObject, "Proc");
        
                     ProcessCreateProcedure(prc);
                     CurrentType = "";
                     
                     break;
                case "SelectStatement":
                     ProcessSelectStatement((SelectStatement)fragment);
                     break;
                case "BeginEndBlockStatement":
                     ProcessBeginEndBlockStatement((BeginEndBlockStatement)fragment);
                     break;
                case "TryCatchStatement":
                     ProcessTryCatchStatement((TryCatchStatement)fragment);
                     break;
                case "UpdateStatement":
                     ProcessUpdateStatement((UpdateStatement)fragment);
                     break;
                case "InsertStatement":
                     ProcessInsertStatement((InsertStatement)fragment);
                     break;

                case "IfStatement":
                     ProcessIfStatement((IfStatement)fragment);
                     break;
                case "BeginTransactionStatement":
                      break;
                default:
                     break;
            }
        }

        public void Prc(string DacPac,string DGMLOut,string DatabaseName,string SchemaName)
        {
            this.databaseName = DatabaseName;
            this.schemaName = SchemaName;

            using (TSqlModel model = TSqlModel.LoadFromDacpac(DacPac,
               new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: true)))
            {

                ModelTypeClass[] Filter = new[]{
                    ModelSchema.View,
                    ModelSchema.Procedure,
                    ModelSchema.Table
                };

                foreach (var vw in model.GetObjects(DacQueryScopes.All, Filter))
                {
                    TSqlFragment frg;
                    if (TSqlModelUtils.TryGetFragmentForAnalysis(vw, out frg))
                    {

                        ProcessTsqlFragment(frg);


                    }
                    int g = 0;
                }
            }
            graph.Save(DGMLOut);


        }

    }

        class Program
        { 
         static void Main(string[] args)
        {

            string DacPac = "";
            string DGML = "";

            string DatabaseName= "";
            string SchemaName = "";

            foreach (var arg in args)
            {
                if (arg.Substring(0, 7).Equals("Dacpac:", StringComparison.OrdinalIgnoreCase))
                {
                    DacPac = arg.Substring(7);
                }
                if (arg.Substring(0, 9).Equals("database:", StringComparison.OrdinalIgnoreCase))
                {
                    DatabaseName = arg.Substring(9);
                }
                if (arg.Substring(0, 5).Equals("DGML:", StringComparison.OrdinalIgnoreCase))
                {
                    DGML = arg.Substring(5);
                }
                if (arg.Substring(0, 7).Equals("Schema:", StringComparison.OrdinalIgnoreCase))
                {
                    SchemaName = arg.Substring(7);
                }
            }
            try
            {
                
                if (DacPac == "" || DGML == "" || DatabaseName == "" || SchemaName == "")
                {
                    Console.WriteLine("Options Dacpac:<Dacpac> DGML:<OutputFile> Database:<DefaultDbName> Schema:<DefaultSchemaName>");
                    return;
                }
                var Dependancy = new ViewDependancy.VwGraph();
                Dependancy.Prc(DacPac, DGML,DatabaseName,SchemaName);
            }
            catch
            {
                Console.WriteLine("Options Dacpac:<Dacpac> DGML:<OutputFile> Database:<DefaultDbName> Schema:<DefaultSchemaName>");
                return;
            }

         }
    }
}

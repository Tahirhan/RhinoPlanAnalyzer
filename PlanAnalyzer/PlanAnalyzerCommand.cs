using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;
using Rhino.Input.Custom;

namespace PlanAnalyzer
{
    public class PlanAnalyzerCommand : Command
    {
        public PlanAnalyzerCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static PlanAnalyzerCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "PlanAnalyzerCommand"; }
        }

        public class GridNode {

            internal int index;
            internal int intCounter;
            internal double currentIntArea;
            internal Curve boundingCrv;
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            List<GridNode> grid = getEmptyGrid(doc);
            List<Curve> nurseCrvs = getNurseCurves(doc);
            processIntersections(ref grid, nurseCrvs);
            placeGrid(doc, grid);
            doc.Views.Redraw();
            return Result.Success;
        }

        private void placeGrid(RhinoDoc doc, List<GridNode> grid)
        {
            foreach (GridNode node in grid)
            {
                Brep brep = Brep.CreatePlanarBreps(node.boundingCrv, 0.1)[0];
                Guid guid = doc.Objects.Add(brep);
                RhinoObject obj = new ObjRef(guid).Object();
                obj.Attributes.ColorSource = ObjectColorSource.ColorFromObject;
                obj.Attributes.ObjectColor = getColor(node.intCounter);
                obj.Attributes.DisplayOrder = 999;
                obj.CommitChanges();
                //doc.Objects.AddCurve(node.boundingCrv, new ObjectAttributes { ColorSource = ObjectColorSource.ColorFromObject, ObjectColor = System.Drawing.Color.Orange });
                doc.Objects.AddText(node.intCounter.ToString(), new Plane(node.boundingCrv.GetBoundingBox(true).Center, Vector3d.XAxis, Vector3d.YAxis), 120, "Arial", true, false, TextJustification.MiddleCenter);
            }
        }

        private Color getColor(int intCounter)
        {
            if (intCounter <= 2) return Color.Blue;
            else if (intCounter <= 5) return Color.Green;
            else if (intCounter <= 8) return Color.Yellow;
            else if (intCounter <= 11) return Color.Orange;
            else return Color.Red;
        }

        private void processIntersections(ref List<GridNode> grid, List<Curve> nurseCrvs)
        {
            foreach (Curve nurseCrv in nurseCrvs)
            {
                Point3d nurseCenter = nurseCrv.GetBoundingBox(true).Center;
                Curve projectedNurseCrv = Curve.ProjectToPlane(nurseCrv, Plane.WorldXY);
                Brep nurseBrep = Brep.CreatePlanarBreps(projectedNurseCrv, 0.1)[0];
                foreach (GridNode node in grid) node.currentIntArea = 0;
                foreach (GridNode node in grid)
                {
                    Brep nodeBrep = Brep.CreatePlanarBreps(node.boundingCrv, 0.1)[0];
                    Intersection.BrepBrep(nurseBrep, nodeBrep, 0.1, out Curve[] intCrvs, out Point3d[] intPts);
                    if (intCrvs == null) continue;
                    if (intCrvs.Count() == 0) continue;
                    node.currentIntArea = nodeBrep.Split(intCrvs, 0.1).OrderBy(r => r.GetBoundingBox(true).Center.DistanceTo(nurseCenter)).First().GetArea();
                    if (node.currentIntArea < 0.1) node.currentIntArea = nodeBrep.GetArea();
                }
                List<GridNode> intNodes = grid.OrderByDescending(r => r.currentIntArea).Take(4).ToList();
                foreach (GridNode node in intNodes) node.intCounter++;
            }
        }

        private List<Curve> getNurseCurves(RhinoDoc doc)
        {
            List<Curve> resCrvs = doc.Objects.FindByDrawColor(System.Drawing.Color.FromArgb(36, 146, 251), false).Where(r=>(r.Geometry as Curve) != null).Where(r=>(r.Geometry as Curve).IsClosed).Select(r=>r.Geometry as Curve).ToList();
            List<Curve> validCrvs = new List<Curve>();
            foreach (Curve crv in resCrvs)
            {
                try
                {
                    Brep brep = Brep.CreatePlanarBreps(crv, 0.1)[0];
                    validCrvs.Add(crv);
                } catch { }
            }
            return validCrvs;
        }

        private List<GridNode> getEmptyGrid(RhinoDoc doc)
        {
            List<GridNode> grid = new List<GridNode>();
            double gridSize = 400;
            double rightTopX = 63980.14 + (9 * gridSize), rightTopY = 31086.91 + (3 * gridSize);
            double currentTopRightX = rightTopX, currentTopRightY = rightTopY;
            int index = 0;
            while (0 < currentTopRightX)
            {
                while (0 < currentTopRightY)
                {
                    grid.Add(new GridNode()
                    {
                        index = index,
                        boundingCrv = new Rectangle3d(Plane.WorldXY, new Point3d(currentTopRightX - gridSize, currentTopRightY - gridSize, 0), new Point3d(currentTopRightX, currentTopRightY, 0)).ToNurbsCurve()
                    });
                    currentTopRightY -= gridSize;
                    index++;
                }
                currentTopRightY = rightTopY;
                currentTopRightX -= gridSize;
            }
            return grid;
        }
    }
}

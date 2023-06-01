﻿using Jastech.Apps.Structure.Data;
using Jastech.Apps.Structure.VisionTool;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Parameters;
using Jastech.Framework.Structure;
using Jastech.Framework.Util.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jastech.Apps.Structure
{
    public class AppsInspModel : InspModel
    {
        [JsonProperty]
        public int UnitCount { get; set; } = 1;

        [JsonProperty]
        public int TabCount { get; set; } = 5;

        [JsonProperty]
        public SpecInfo SpecInfo { get; set; } = new SpecInfo();

        [JsonProperty]
        public MaterialInfo MaterialInfo { get; set; } = new MaterialInfo();

        [JsonProperty]
        public List<Unit> UnitList { get; private set; } = new List<Unit>();

        public Unit GetUnit(string name)
        {
            return UnitList.Where(x => x.Name == name).First();
        }

        public Unit GetUnit(UnitName name)
        {
            return UnitList.Where(x => x.Name == name.ToString()).First();
        }

        public void AddUnit(Unit unit)
        {
            UnitList.Add(unit);
        }

        public List<Unit> GetUnitList()
        {
            return UnitList;
        }

        public void SetUnitList(List<Unit> newUnitList)
        {
            foreach (var unit in UnitList)
                unit.Dispose();

            UnitList.Clear();

            UnitList.AddRange(newUnitList.Select(x => x.DeepCopy()).ToList());
        }
    }
}

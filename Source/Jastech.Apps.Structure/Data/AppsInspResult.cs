﻿using Cognex.VisionPro;
using Emgu.CV;
using Jastech.Framework.Imaging.Result;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Results;
using Jastech.Framework.Macron.Akkon.Results;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jastech.Apps.Structure.Data
{
    public class AppsInspResult
    {
        [JsonIgnore]
        public Mat Image { get; set; } = null;

        [JsonIgnore]
        public ICogImage CogImage { get; set; } = null;

        [JsonIgnore]
        public ICogImage AkkonResultImage { get; set; } = null;

        public int TabNo { get; set; } = -1;

        public MarkResult FpcMark { get; set; } = new MarkResult();

        public MarkResult PanelMark { get; set; } = new MarkResult();

        public AlignResult LeftAlignX { get; set; } = null;

        public AlignResult LeftAlignY { get; set; } = null;

        public AlignResult RightAlignX { get; set; } = null;

        public AlignResult RightAlignY { get; set; } = null;

        [JsonProperty]
        public List<AkkonResult> AkkonResultList { get; set; } = new List<AkkonResult>();


        [JsonProperty]
        public List<AkkonResult> Akkon { get; set; } = null;

        public void Dispose()
        {
            if(Image != null)
            {
                Image.Dispose();
                Image = null;
            }

            if (AkkonResultImage != null)
                AkkonResultImage = null;
        }
    }

    public class MarkResult
    {
        [JsonProperty]
        public Judgement Judement { get; set; } = Judgement.OK;

        public double TranslateX { get; set; } = 0;

        public double TranslateY { get; set; } = 0;

        public double TranslateRotion { get; set; } = 0;

        public MarkMatchingResult FoundedMark { get; set; } = null;

        public List<MarkMatchingResult> FailMarks { get; set; } = new List<MarkMatchingResult>();
    }

    public class MarkMatchingResult
    {
        [JsonProperty]
        public CogPatternMatchingResult Left { get; set; } = null;

        [JsonProperty]
        public CogPatternMatchingResult Right { get; set; } = null;
    }

    public class AlignResult
    {
        public Judgement Judgement { get; set; } = Judgement.OK;

        public CogAlignCaliperResult Panel { get; set; } = null;

        public CogAlignCaliperResult Fpc { get; set; } = null;
    }
}

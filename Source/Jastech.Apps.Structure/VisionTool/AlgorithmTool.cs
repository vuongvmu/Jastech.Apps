﻿using Cognex.VisionPro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Structure.Parameters;
using Jastech.Framework.Imaging;
using Jastech.Framework.Imaging.Helper;
using Jastech.Framework.Imaging.Result;
using Jastech.Framework.Imaging.VisionPro;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Parameters;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Results;
using Jastech.Framework.Macron.Akkon;
using Jastech.Framework.Macron.Akkon.Parameters;
using Jastech.Framework.Macron.Akkon.Results;
using Jastech.Framework.Util.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.CogCaliper;

namespace Jastech.Apps.Structure.VisionTool
{
    public class AlgorithmTool
    {
       
        private CogPatternMatching PatternAlgorithm { get; set; } = new CogPatternMatching();

        public CogAlignCaliper AlignAlgorithm { get; set; } = new CogAlignCaliper();

       public ICogImage ConvertCogImage(Mat image)
        {
            if (image == null)
                return null;

            int size = image.Width * image.Height * image.NumberOfChannels;
            byte[] dataArray = new byte[size];
            Marshal.Copy(image.DataPointer, dataArray, 0, size);
            ColorFormat format = image.NumberOfChannels == 1 ? ColorFormat.Gray : ColorFormat.RGB24;
            var cogImage = CogImageHelper.CovertImage(dataArray, image.Width, image.Height, format);

            return cogImage;
        }

        public CogAlignCaliperResult RunAlignX(ICogImage image, VisionProCaliperParam param, int leadCount)
        {
            if (image == null || param == null)
                return null;

            CogAlignCaliperResult alignResult = new CogAlignCaliperResult();
            alignResult.AddAlignResult(AlignAlgorithm.RunAlignX(image, param, leadCount));

            bool isFounded = false;
            foreach (var item in alignResult.CogAlignResult)
            {
                isFounded |= item.Found;
            }

            alignResult.Judgement = isFounded ? Judgement.OK : Judgement.Fail;

            if(alignResult.Judgement == Judgement.OK)
            {
                if (leadCount != alignResult.CogAlignResult.Count() / 2)
                    alignResult.Judgement = Judgement.NG;
            }
                
            return alignResult;
        }

        public CogAlignCaliperResult RunAlignY(ICogImage image, VisionProCaliperParam param)
        {
            CogAlignCaliperResult alignResult = new CogAlignCaliperResult();
            var result = AlignAlgorithm.RunAlignY(image, param);
            alignResult.AddAlignResult(result);

            bool isFounded = false;
            foreach (var item in alignResult.CogAlignResult)
            {
                if (item == null)
                    continue;

                isFounded |= item.Found;
            }

            alignResult.Judgement = isFounded ? Judgement.OK : Judgement.Fail;

            return alignResult;
        }

        public CogPatternMatchingResult RunPatternMatch(ICogImage image, VisionProPatternMatchingParam param)
        {
            if (image == null || param == null)
                return null;

            CogPatternMatchingResult matchingResult = PatternAlgorithm.Run(image, param);

            if (matchingResult == null)
                return null;

            if (matchingResult.MatchPosList.Count <= 0)
                matchingResult.Judgement = Judgement.Fail;
            else
            {
                if ((matchingResult.MaxScore * 100) >= param.Score)
                    matchingResult.Judgement = Judgement.OK;
                else
                    matchingResult.Judgement = Judgement.NG;
            }

            return matchingResult;
        }
    }

    public enum InspectionType
    {
        PreAlign,
        Align,
        Akkon,
    }

    public enum AlignName
    {
        Tab1,
    }
}

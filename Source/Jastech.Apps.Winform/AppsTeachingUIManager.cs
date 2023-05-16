﻿using Cognex.VisionPro;
using Emgu.CV;
using Jastech.Framework.Imaging.Helper;
using Jastech.Framework.Winform.VisionPro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jastech.Apps.Winform
{
    public class AppsTeachingUIManager
    {
        #region 필드
        private static AppsTeachingUIManager _instance = null;
        #endregion

        #region 속성
        private CogDisplayControl TeachingDisplay { get; set; } = null;

        private ICogImage OrginCogImageBuffer { get; set; } = null;

        private Mat OriginMatImageBuffer { get; set; } = null;

        private ICogImage BinaryCogImageBuffer { get; set; } = null;

        private ICogImage ResultCogImageBuffer { get; set; } = null;

        //public Mat PrevMatImage { get; private set; } = null;

        //public ICogImage PrevResultImage { get; private set; } = null;
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        #endregion

        #region 메서드
        public static AppsTeachingUIManager Instance()
        {
            if (_instance == null)
            {
                _instance = new AppsTeachingUIManager();
            }

            return _instance;
        }

        public CogDisplayControl GetDisplay()
        {
            return TeachingDisplay;
        }

        public void SetDisplay(CogDisplayControl display)
        {
            TeachingDisplay = display;
        }

        public ICogImage GetOriginCogImageBuffer(bool isDeepCopy)
        {
            if (isDeepCopy)
            {
                if (OrginCogImageBuffer == null)
                    return null;

                return OrginCogImageBuffer.CopyBase(CogImageCopyModeConstants.CopyPixels);
            }

            return OrginCogImageBuffer;
        }

        public void SetOrginCogImageBuffer(ICogImage cogImage)
        {
            OrginCogImageBuffer = null;
            BinaryCogImageBuffer = null;
            ResultCogImageBuffer = null;
            OrginCogImageBuffer = cogImage.CopyBase(CogImageCopyModeConstants.CopyPixels);

            if (OriginMatImageBuffer != null)
            {
                OriginMatImageBuffer.Dispose();
                OriginMatImageBuffer = null;
            }

            TeachingDisplay?.SetImage(cogImage);
        }

        public void SetOriginMatImageBuffer(Mat mat)
        {
            if(OriginMatImageBuffer != null)
            {
                OriginMatImageBuffer.Dispose();
                OriginMatImageBuffer = null;
            }
            OriginMatImageBuffer = mat;
        }

        public Mat GetOriginMatImageBuffer(bool isDeepCopy)
        {
            if(isDeepCopy)
                return MatHelper.DeepCopy(OriginMatImageBuffer);

            return OriginMatImageBuffer;
        }

        public void SetBinaryCogImageBuffer(ICogImage cogImage)
        {
            BinaryCogImageBuffer = cogImage;
            TeachingDisplay?.SetImage(BinaryCogImageBuffer);
        }

        public ICogImage GetBinaryCogImage(bool isDeepCopy)
        {
            if (isDeepCopy)
            {
                if (BinaryCogImageBuffer == null)
                    return null;

                return BinaryCogImageBuffer.CopyBase(CogImageCopyModeConstants.CopyPixels);
            }

            return BinaryCogImageBuffer;
        }

        public void SetResultCogImage(ICogImage cogImage)
        {
            if (ResultCogImageBuffer != null)
                ResultCogImageBuffer = null;

            ResultCogImageBuffer = cogImage.CopyBase(CogImageCopyModeConstants.CopyPixels);
            TeachingDisplay?.SetImage(ResultCogImageBuffer);
        }

        public ICogImage GetResultCogImage(bool isDeepCopy)
        {
            if (isDeepCopy)
            {
                if (ResultCogImageBuffer == null)
                    return null;

                return ResultCogImageBuffer.CopyBase(CogImageCopyModeConstants.CopyPixels);
            }

            return ResultCogImageBuffer;
        }
        #endregion

    }
}

﻿using Jastech.Framework.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jastech.Apps.Winform.Settings
{
    public class Settings : ConfigSet
    {
        #region 필드 
        private static Settings _instance = null;
        #endregion

        public static Settings Instance()
        {
            if (_instance == null)
            {
                _instance = new Settings();
            }

            return _instance;
        }

        public override void Initialize()
        {
            base.PathConfigCreated += Settings_PathConfigCreated;
            base.Initialize();
        }

        private void Settings_PathConfigCreated(PathConfig config)
        {
            config.CreateDirectory();
        }

        public override void Save()
        {
            base.Save();
        }

        public override bool Load()
        {
            return base.Load();
        }
    }
}

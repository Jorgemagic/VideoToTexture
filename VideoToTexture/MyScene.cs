using Evergine.Common.Graphics;
using Evergine.Framework;
using System;
using System.IO;
using VideoToTexture.Components;

namespace VideoToTexture
{
    public class MyScene : Scene
    {
        public override void RegisterManagers()
        {
            base.RegisterManagers();
            this.Managers.AddManager(new global::Evergine.Bullet.BulletPhysicManager3D());
        }

        protected override void CreateScene()
        {                             
        }
    }
}



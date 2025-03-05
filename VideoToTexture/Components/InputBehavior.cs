using Evergine.Common.Input;
using Evergine.Common.Input.Keyboard;
using Evergine.Framework;
using Evergine.Framework.Services;
using System;
using System.Diagnostics;

namespace VideoToTexture.Components
{
    public class InputBehavior : Behavior
    {
        [BindService]
        protected GraphicsPresenter graphicsPresenter = null;

        [BindComponent]
        private VideoPlayer videoPlayer = null;

        protected override void Update(TimeSpan gameTime)
        {
            KeyboardDispatcher keyboardDispatcher = this.graphicsPresenter.FocusedDisplay?.KeyboardDispatcher;

            if (keyboardDispatcher?.ReadKeyState(Keys.P) == ButtonState.Pressed)
            {
                this.videoPlayer.Pause();
            }
            else if (keyboardDispatcher.ReadKeyState(Keys.Space) == ButtonState.Pressed)
            {
                this.videoPlayer.Play();
            }
            else if (keyboardDispatcher.ReadKeyState(Keys.O) == ButtonState.Pressed)
            {
                this.videoPlayer.Stop();
            }
            else if (keyboardDispatcher.ReadKeyState(Keys.N) == ButtonState.Pressed)
            {
                this.videoPlayer.VideoPath = "Videos/fireworks.mp4";                
            }

            Debug.WriteLine($"W:{this.videoPlayer.VideoWidth} H: {this.videoPlayer.VideoHeight}");
        }
    }
}

using Evergine.Common.Input;
using Evergine.Common.Input.Keyboard;
using Evergine.Framework;
using Evergine.Framework.Services;
using System;

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
                this.videoPlayer.Play(true);
            }
            else if (keyboardDispatcher.ReadKeyState(Keys.O) == ButtonState.Pressed)
            {
                this.videoPlayer.Stop();
            }
        }
    }
}

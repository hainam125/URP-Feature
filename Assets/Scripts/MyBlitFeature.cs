using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MyBlitFeature : ScriptableRendererFeature {
    [System.Serializable]
    public class MyFeatureSettings {
        public RenderPassEvent WhenToInsert = RenderPassEvent.AfterRendering;
        public Material MaterialToBlit;
    }

    // MUST be named "settings" (lowercase) to be shown in the Render Features inspector
    public MyFeatureSettings settings = new MyFeatureSettings();

    MyBlitRenderPass myRenderPass;

    public override void Create() {
        myRenderPass = new MyBlitRenderPass(
          "My custom pass",
          settings.WhenToInsert,
          settings.MaterialToBlit
        );
    }

    // called every frame once per camera
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        // Gather up and pass any extra information our pass will need.
        // In this case we're getting the camera's color buffer target
        var cameraColorTargetIdent = renderer.cameraColorTarget;
        myRenderPass.Setup(cameraColorTargetIdent);

        // Ask the renderer to add our pass.
        // Could queue up multiple passes and/or pick passes to use
        renderer.EnqueuePass(myRenderPass);
    }

    private class MyBlitRenderPass : ScriptableRenderPass {
        // used to label this pass in Unity's Frame Debug utility
        string profilerTag;

        Material materialToBlit;
        RenderTargetIdentifier cameraColorTargetIdent;
        RenderTargetHandle tempTexture;

        public MyBlitRenderPass(string profilerTag, RenderPassEvent renderPassEvent, Material materialToBlit) {
            this.profilerTag = profilerTag;
            this.renderPassEvent = renderPassEvent;
            this.materialToBlit = materialToBlit;
        }

        // This isn't part of the ScriptableRenderPass class and is our own addition.
        // For this custom pass we need the camera's color target, so that gets passed in.
        public void Setup(RenderTargetIdentifier cameraColorTargetIdent) {
            this.cameraColorTargetIdent = cameraColorTargetIdent;
        }

        // called each frame before Execute, use it to set up things the pass will need
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
            // create a temporary render texture that matches the camera
            cmd.GetTemporaryRT(tempTexture.id, cameraTextureDescriptor);
        }

        // Execute is called for every eligible camera every frame. It's not called at the moment that
        // rendering is actually taking place, so don't directly execute rendering commands here.
        // Instead use the methods on ScriptableRenderContext to set up instructions.
        // RenderingData provides a bunch of (not very well documented) information about the scene
        // and what's being rendered.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            // fetch a command buffer to use
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
            cmd.Clear();

            // the actual content of our custom render pass!
            // we apply our material while blitting to a temporary texture
            cmd.Blit(cameraColorTargetIdent, tempTexture.Identifier(), materialToBlit, 0);

            // ...then blit it back again 
            cmd.Blit(tempTexture.Identifier(), cameraColorTargetIdent);

            // don't forget to tell ScriptableRenderContext to actually execute the commands
            context.ExecuteCommandBuffer(cmd);

            // tidy up after ourselves
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // called after Execute, use it to clean up anything allocated in Configure
        public override void FrameCleanup(CommandBuffer cmd) {
            cmd.ReleaseTemporaryRT(tempTexture.id);
        }
    }
}

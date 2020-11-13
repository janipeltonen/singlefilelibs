using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


/*
Simple 2D camera for XNA/MONO framework
Just for practice in struct/class semantics this was made to be a struct so careful with the use of REF and not copying it by value.

*/

namespace XNACamera
{

    public enum CameraState
    {
        Free,
        Follow,
    }

    public struct Camera
    {
        public Vector2 position;
        public CameraState state;
        public float zoom;
        float speed;
        float zoomtarget;
        float zoompersecond;
        Vector2 velocity;

        public short targetindex; //global entity index targetille

        const float MAX_ZOOM = 6;
        const float MIN_ZOOM = 0.5f;


        public Matrix Transformation
        {
            get
            {
                return Matrix.CreateScale(zoom) * Matrix.CreateTranslation(new Vector3(-position, 0f));
            }
        }

        public Matrix InverseTransform => Matrix.Invert(Transformation);


        public Camera(Vector2 pos, float zoom, float speed, CameraState initialstate, short targetindex = -1)
        {
            position = pos;
            state = initialstate;
            zoomtarget = zoom;
            zoompersecond = 1f;
            velocity = Vector2.Zero;
            this.zoom = zoom;
            this.speed = speed;
            this.targetindex = targetindex;
        }

        public void UpdateCamera(float deltatime, in Vector2 direction)
        {
            //update zoom
            if (Zooming)
            {
                float dzoom = deltatime * zoompersecond;

                //nää on if blockeja vaikka vois tehä erilailla koska haluan selvän blockin ku target on reached
                if (zoom < zoomtarget)
                {
                    zoom += dzoom;
                    if (zoom > zoomtarget)
                        zoom = (zoomtarget);
                }
                else if (zoom > zoomtarget)
                {
                    zoom -= dzoom;
                    if (zoom < zoomtarget)
                        zoom = zoomtarget;
                }
            }

            this.position += direction * speed * deltatime;
        }



        public void AddCameraZoom(float z, float zps = 1f)
        {
            zoomtarget += z;
            //ei anneta zoomin mennä ikinä näitten yli tai ali
            if (zoomtarget > MAX_ZOOM) zoomtarget = MAX_ZOOM;
            if (zoomtarget < MIN_ZOOM) zoomtarget = MIN_ZOOM;
            zoompersecond = zps;
        }

        private bool Zooming => (zoom != zoomtarget);


    }

}

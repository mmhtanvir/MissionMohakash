        public GameObject MakeBeacon(Vector3 at, Color c, float height = 14f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Beacon";
            go.transform.SetParent(transform);
            go.transform.position = at + Vector3.up * height / 2;
            go.transform.localScale = new Vector3(0.35f, height / 2, 0.35f);
            var r = go.GetComponent<Renderer>();
            var beamMat = Resources.Load<Material>("Materials/Beam");
            if (beamMat == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s != null)
                    beamMat = new Material(s);
                else
                    beamMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            r.material = new Material(beamMat) { color = c };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

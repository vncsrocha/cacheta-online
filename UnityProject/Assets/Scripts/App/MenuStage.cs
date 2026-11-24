using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// O "palco" 3D atrás dos menus: o ambiente e o seu personagem. No menu inicial o personagem fica à esquerda,
    /// abaixo do nome do jogo; no editor de perfil ele vem para perto da câmera.
    /// </summary>
    public class MenuStage
    {
        public enum Framing { Menu, Profile, Centered }

        private readonly GameApp _app;
        private readonly GameObject _root;
        private GameObject _env;
        private GameObject _avatar;
        private EnvironmentId _envId;
        private Framing _framing = Framing.Menu;
        private float _spin;

        public MenuStage(GameApp app)
        {
            _app = app;
            _root = new GameObject("Palco do menu");
            _app.Cam.fieldOfView = 50f;
            SetEnvironment(EnvironmentId.QuintalAoEntardecer);
            SetAvatar(app.Profile);
        }

        public void SetEnvironment(EnvironmentId id)
        {
            if (_env != null && _envId == id) return;
            if (_env != null) Object.Destroy(_env);
            _envId = id;
            _env = new GameObject("Ambiente");
            _env.transform.SetParent(_root.transform, false);
            EnvironmentBuilder.Build(id, _env.transform, _app.Cam);
        }

        public void SetAvatar(PlayerProfile p)
        {
            if (_avatar == null)
            {
                _avatar = AvatarBuilder.Build(p, _root.transform, seated: false);
            }
            else AvatarBuilder.Rebuild(_avatar, p, seated: false);
        }

        public void SetFraming(Framing f) => _framing = f;

        public void Tick()
        {
            if (_avatar == null) return;
            Vector3 avatarPos, camPos, look;
            switch (_framing)
            {
                case Framing.Profile:
                    avatarPos = new Vector3(-0.75f, 0, 0);
                    camPos = new Vector3(0, 1.25f, -3.6f);
                    look = new Vector3(0, 1.05f, 0);
                    break;
                case Framing.Centered:
                    avatarPos = new Vector3(-2.7f, 0, 0);
                    camPos = new Vector3(0, 1.5f, -6.5f);
                    look = new Vector3(0, 1.5f, 0);
                    break;
                default:
                    avatarPos = new Vector3(-2.55f, 0, 0);
                    camPos = new Vector3(0, 1.45f, -6.5f);
                    look = new Vector3(0, 1.45f, 0);
                    break;
            }
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            _avatar.transform.position = Vector3.Lerp(_avatar.transform.position, avatarPos, k);
            _spin += Time.deltaTime;
            // Olha para a câmera (o personagem olha para +Z) com um balanço leve.
            _avatar.transform.rotation = Quaternion.Euler(0, 180f + Mathf.Sin(_spin * 0.7f) * 20f, 0);

            var cam = _app.Cam.transform;
            cam.position = Vector3.Lerp(cam.position, camPos, k);
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(look - camPos), k);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }
    }
}

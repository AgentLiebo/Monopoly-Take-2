#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyTake2;

[RequireComponent(typeof(UnityGameController))]
public sealed class UnityMonopolyBoardView : MonoBehaviour
{
    [Header("3D Board")]
    [SerializeField] private float boardRadius = 11f;
    [SerializeField] private float tileSize = 1.75f;
    [SerializeField] private float tokenRadius = 0.35f;
    [SerializeField] private float cameraHeight = 18f;
    [SerializeField] private bool createCameraAndLight = true;

    private UnityGameController _controller = null!;
    private readonly Dictionary<int, Transform> _spaceRoots = new();
    private readonly Dictionary<Guid, Transform> _tokens = new();
    private readonly Dictionary<int, Renderer> _ownershipRenderers = new();
    private readonly Dictionary<int, List<GameObject>> _improvementObjects = new();
    private readonly Dictionary<ColorGroup, Color> _groupColors = new();
    private Material _defaultMaterial = null!;
    private Material _selectedMaterial = null!;
    private Material _ownedMaterial = null!;

    private void Awake()
    {
        _controller = GetComponent<UnityGameController>();
        BuildPalette();
        BuildMaterials();
        if (createCameraAndLight)
        {
            EnsureCameraAndLight();
        }
    }

    private void OnEnable()
    {
        _controller.GameRefreshed += RefreshBoard;
    }

    private void OnDisable()
    {
        _controller.GameRefreshed -= RefreshBoard;
    }

    private void Start()
    {
        BuildBoard();
        RefreshBoard();
    }

    private void Update()
    {
        if (Camera.main == null || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 200f))
        {
            return;
        }

        var selectable = hit.collider.GetComponentInParent<BoardSpaceSelector>();
        if (selectable != null)
        {
            _controller.SelectSpace(selectable.BoardIndex);
        }
    }

    private void BuildBoard()
    {
        if (_spaceRoots.Count > 0)
        {
            return;
        }

        var root = new GameObject("Runtime 3D Monopoly Board").transform;
        root.SetParent(transform, false);

        for (var i = 0; i < MonopolyRules.BoardSpaceCount; i++)
        {
            var space = _controller.Game.Board.GetSpace(i);
            var spaceRoot = new GameObject($"{i:00} - {space.Name}").transform;
            spaceRoot.SetParent(root, false);
            spaceRoot.position = BoardPosition(i);
            spaceRoot.rotation = Quaternion.LookRotation(Vector3.zero - spaceRoot.position, Vector3.up);
            _spaceRoots[i] = spaceRoot;

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Tile";
            tile.transform.SetParent(spaceRoot, false);
            tile.transform.localScale = new Vector3(tileSize, 0.18f, tileSize);
            tile.transform.localPosition = Vector3.zero;
            tile.AddComponent<BoardSpaceSelector>().BoardIndex = i;
            var tileRenderer = tile.GetComponent<Renderer>();
            tileRenderer.sharedMaterial = CreateMaterial(GetSpaceColor(space));

            var ownerMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownerMarker.name = "Owner Marker";
            ownerMarker.transform.SetParent(spaceRoot, false);
            ownerMarker.transform.localScale = new Vector3(tileSize * 0.75f, 0.08f, 0.22f);
            ownerMarker.transform.localPosition = new Vector3(0f, 0.16f, -tileSize * 0.35f);
            _ownershipRenderers[i] = ownerMarker.GetComponent<Renderer>();
            _ownershipRenderers[i].sharedMaterial = _defaultMaterial;

            var label = new GameObject("Label");
            label.transform.SetParent(spaceRoot, false);
            label.transform.localPosition = new Vector3(0f, 0.3f, 0.1f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = ShortName(space.Name);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.18f;
            textMesh.fontSize = 42;
            textMesh.color = Color.black;

            _improvementObjects[i] = new List<GameObject>(5);
        }
    }

    private void RefreshBoard()
    {
        if (_spaceRoots.Count == 0 || _controller.CurrentUi == null)
        {
            return;
        }

        RefreshSelection();
        RefreshOwnershipAndImprovements();
        RefreshTokens();
    }

    private void RefreshSelection()
    {
        for (var i = 0; i < MonopolyRules.BoardSpaceCount; i++)
        {
            var tileRenderer = _spaceRoots[i].Find("Tile")!.GetComponent<Renderer>();
            if (i == _controller.SelectedSpaceIndex)
            {
                tileRenderer.sharedMaterial = _selectedMaterial;
            }
            else
            {
                tileRenderer.sharedMaterial = CreateMaterial(GetSpaceColor(_controller.Game.Board.GetSpace(i)));
            }
        }
    }

    private void RefreshOwnershipAndImprovements()
    {
        foreach (var propertyPair in _controller.Game.State.Properties)
        {
            var property = propertyPair.Value;
            var boardIndex = propertyPair.Key;
            var ownerMarker = _ownershipRenderers[boardIndex];
            ownerMarker.sharedMaterial = property.OwnerId.HasValue ? _ownedMaterial : _defaultMaterial;
            ownerMarker.enabled = property.OwnerId.HasValue;

            ClearImprovements(boardIndex);
            if (property.IsMortgaged)
            {
                CreateImprovement(boardIndex, 0, Color.gray, 0.55f, "M");
            }
            else if (property.HasHotel)
            {
                CreateImprovement(boardIndex, 0, Color.red, 0.65f, "Hotel");
            }
            else
            {
                for (var i = 0; i < property.Houses; i++)
                {
                    CreateImprovement(boardIndex, i, Color.green, 0.35f, "House");
                }
            }
        }
    }

    private void RefreshTokens()
    {
        for (var i = 0; i < _controller.Game.State.Players.Count; i++)
        {
            var player = _controller.Game.State.Players[i];
            if (!_tokens.TryGetValue(player.Id, out var token))
            {
                token = CreateToken(player, i);
                _tokens[player.Id] = token;
            }

            var basePosition = BoardPosition(player.Position);
            var offset = TokenOffset(i);
            token.position = Vector3.Lerp(token.position, basePosition + offset + Vector3.up * 0.75f, Time.deltaTime > 0f ? 0.35f : 1f);
            token.gameObject.SetActive(!player.Bankrupt);
        }
    }

    private Transform CreateToken(PlayerState player, int playerIndex)
    {
        var token = GameObject.CreatePrimitive(GetTokenPrimitive(player.Token));
        token.name = $"Token - {player.Name}";
        token.transform.localScale = Vector3.one * tokenRadius;
        token.GetComponent<Renderer>().sharedMaterial = CreateMaterial(PlayerColor(playerIndex));
        return token.transform;
    }

    private void ClearImprovements(int boardIndex)
    {
        var list = _improvementObjects[boardIndex];
        for (var i = 0; i < list.Count; i++)
        {
            Destroy(list[i]);
        }
        list.Clear();
    }

    private void CreateImprovement(int boardIndex, int slot, Color color, float height, string name)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.SetParent(_spaceRoots[boardIndex], false);
        marker.transform.localScale = new Vector3(0.25f, height, 0.25f);
        marker.transform.localPosition = new Vector3(-0.55f + slot * 0.28f, 0.32f + height * 0.5f, 0.48f);
        marker.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
        _improvementObjects[boardIndex].Add(marker);
    }

    private Vector3 BoardPosition(int index)
    {
        var side = index / 10;
        var step = index % 10;
        var t = step / 10f;
        return side switch
        {
            0 => new Vector3(boardRadius - t * boardRadius * 2f, 0f, -boardRadius),
            1 => new Vector3(-boardRadius, 0f, -boardRadius + t * boardRadius * 2f),
            2 => new Vector3(-boardRadius + t * boardRadius * 2f, 0f, boardRadius),
            _ => new Vector3(boardRadius, 0f, boardRadius - t * boardRadius * 2f)
        };
    }

    private Vector3 TokenOffset(int playerIndex)
    {
        var row = playerIndex / 4;
        var column = playerIndex % 4;
        return new Vector3(-0.45f + column * 0.3f, 0f, -0.25f + row * 0.3f);
    }

    private static PrimitiveType GetTokenPrimitive(TokenType token)
    {
        return token switch
        {
            TokenType.Car or TokenType.Battleship => PrimitiveType.Cube,
            TokenType.Hat or TokenType.Thimble => PrimitiveType.Cylinder,
            _ => PrimitiveType.Sphere
        };
    }

    private Color GetSpaceColor(BoardSpaceData space)
    {
        if (_groupColors.TryGetValue(space.ColorGroup, out var color))
        {
            return color;
        }

        return space.Type switch
        {
            SpaceType.Go => new Color(0.9f, 1f, 0.9f),
            SpaceType.Chance => new Color(1f, 0.85f, 0.4f),
            SpaceType.CommunityChest => new Color(0.55f, 0.85f, 1f),
            SpaceType.IncomeTax or SpaceType.LuxuryTax => new Color(1f, 0.65f, 0.65f),
            SpaceType.Jail or SpaceType.GoToJail => new Color(1f, 0.75f, 0.45f),
            SpaceType.FreeParking => new Color(0.75f, 1f, 0.75f),
            _ => Color.white
        };
    }

    private void BuildPalette()
    {
        _groupColors[ColorGroup.Brown] = new Color(0.45f, 0.24f, 0.1f);
        _groupColors[ColorGroup.LightBlue] = new Color(0.45f, 0.8f, 1f);
        _groupColors[ColorGroup.Pink] = new Color(1f, 0.45f, 0.8f);
        _groupColors[ColorGroup.Orange] = new Color(1f, 0.55f, 0.2f);
        _groupColors[ColorGroup.Red] = new Color(0.9f, 0.1f, 0.1f);
        _groupColors[ColorGroup.Yellow] = new Color(1f, 0.95f, 0.25f);
        _groupColors[ColorGroup.Green] = new Color(0.1f, 0.65f, 0.25f);
        _groupColors[ColorGroup.DarkBlue] = new Color(0.1f, 0.2f, 0.75f);
        _groupColors[ColorGroup.Railroad] = new Color(0.75f, 0.75f, 0.75f);
        _groupColors[ColorGroup.Utility] = new Color(0.7f, 0.55f, 1f);
    }

    private void BuildMaterials()
    {
        _defaultMaterial = CreateMaterial(Color.white);
        _selectedMaterial = CreateMaterial(new Color(1f, 1f, 0.25f));
        _ownedMaterial = CreateMaterial(new Color(0.15f, 0.25f, 0.95f));
    }

    private static Material CreateMaterial(Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    private static Color PlayerColor(int index)
    {
        return index switch
        {
            0 => Color.red,
            1 => Color.blue,
            2 => Color.green,
            3 => Color.magenta,
            4 => Color.cyan,
            5 => Color.yellow,
            6 => new Color(1f, 0.5f, 0f),
            _ => Color.black
        };
    }

    private static string ShortName(string name)
    {
        return name.Length <= 14 ? name : name.Replace(" Avenue", string.Empty).Replace(" Railroad", " RR").Replace("Community Chest", "Chest");
    }

    private void EnsureCameraAndLight()
    {
        if (Camera.main == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, cameraHeight, -17f);
            camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            camera.fieldOfView = 48f;
        }

        if (FindObjectOfType<Light>() == null)
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    private sealed class BoardSpaceSelector : MonoBehaviour
    {
        public int BoardIndex { get; set; }
    }
}
#endif

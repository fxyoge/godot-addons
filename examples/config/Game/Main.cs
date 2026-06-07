using System;
using System.Collections.Generic;
using Fxyoge.DependencyInjection;
using Fxyoge.DependencyInjection.Configuration;
using Godot;

namespace ConfigExample.Game;

public partial class Main : Control
{
    private const float Gravity = 2100f;
    private const float JumpVelocity = -760f;
    private const float PlayerSize = 38f;
    private const float GroundHeight = 150f;
    private const float SplashRadius = 126f;
    private const float ExplosionDuration = 0.34f;
    private const float DamageNumberDuration = 0.72f;
    private const int ToneSampleRate = 22050;

    private readonly List<Enemy> _enemies = new();
    private readonly List<Explosion> _explosions = new();
    private readonly List<DamageNumber> _damageNumbers = new();
    private readonly RandomNumberGenerator _random = new();

    private ISettingsMonitor<GameplayOptions>? _gameplay;

    private ConfigMenu? _configMenu;
    private Button? _configButton;
    private Label? _scoreLabel;
    private AudioStreamPlayer? _jumpSound;
    private AudioStreamPlayer? _landSound;
    private AudioStreamPlayer? _hitSound;

    private Vector2 _playerPosition;
    private float _playerVelocityY;
    private float _spawnTimer;
    private bool _grounded = true;
    private bool _jumpWasPressed;
    private bool _isReady;
    private int _score;

    public override void _EnterTree()
    {
        SetProcess(false);
    }

    public override void _Ready()
    {
        SetProcess(false);

        var services = GetTree()
            .Root
            .GetNode<GameServices>("GameServices");

        _ = services.GetRequiredService<ISettingsMonitor<InputOptions>>();
        _gameplay = services.GetRequiredService<ISettingsMonitor<GameplayOptions>>();

        _random.Randomize();
        BindSceneNodes();
        ConnectUi();
        ConnectMonitors();
        ResetArena();
        RefreshUi();
        _isReady = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!_isReady)
        {
            return;
        }

        var seconds = (float)delta;
        var wasGrounded = _grounded;
        var jumpPressed = Input.IsActionPressed("jump");

        if (_grounded && jumpPressed && !_jumpWasPressed)
        {
            _playerVelocityY = JumpVelocity;
            _grounded = false;
            PlayTone(_jumpSound, 620f, 0.1f, 0.22f);
        }

        _jumpWasPressed = jumpPressed;
        _playerVelocityY += Gravity * seconds;
        _playerPosition.Y += _playerVelocityY * seconds;

        var groundY = GetGroundY();
        if (_playerPosition.Y >= groundY)
        {
            _playerPosition.Y = groundY;
            _playerVelocityY = 0;
            _grounded = true;

            if (!wasGrounded)
            {
                Land();
            }
        }

        UpdateEnemies(seconds);
        UpdateExplosions(seconds);
        UpdateDamageNumbers(seconds);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = GetViewportRect().Size;
        var groundY = GetGroundY();

        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.47f, 0.74f, 0.96f));
        DrawRect(new Rect2(0, groundY, size.X, GroundHeight), new Color(0.43f, 0.25f, 0.12f));
        DrawLine(new Vector2(0, groundY), new Vector2(size.X, groundY), new Color(0.24f, 0.54f, 0.22f), 8f);

        foreach (var enemy in _enemies)
        {
            var half = enemy.Size * 0.5f;
            DrawColoredPolygon(
                new[]
                {
                    new Vector2(enemy.Position.X, enemy.Position.Y - enemy.Size),
                    new Vector2(enemy.Position.X - half, enemy.Position.Y),
                    new Vector2(enemy.Position.X + half, enemy.Position.Y),
                },
                new Color(0.11f, 0.62f, 0.21f));
        }

        foreach (var explosion in _explosions)
        {
            var progress = 1f - (explosion.TimeLeft / ExplosionDuration);
            var radius = Mathf.Lerp(20f, SplashRadius, progress);
            var alpha = 1f - progress;
            DrawCircle(explosion.Position, radius, new Color(1f, 0.86f, 0.22f, 0.18f * alpha));
            DrawArc(explosion.Position, radius, 0, Mathf.Tau, 64, new Color(1f, 0.45f, 0.12f, alpha), 5f);
        }

        foreach (var damageNumber in _damageNumbers)
        {
            var progress = 1f - (damageNumber.TimeLeft / DamageNumberDuration);
            var alpha = 1f - progress;
            DrawString(
                GetThemeDefaultFont(),
                damageNumber.Position,
                damageNumber.Text,
                HorizontalAlignment.Center,
                90,
                24,
                new Color(1f, 0.95f, 0.2f, alpha));
        }

        var playerRect = new Rect2(
            _playerPosition.X - PlayerSize * 0.5f,
            _playerPosition.Y - PlayerSize,
            PlayerSize,
            PlayerSize);
        DrawRect(playerRect, new Color(0.9f, 0.08f, 0.08f));
    }

    private void BindSceneNodes()
    {
        _scoreLabel = GetNode<Label>("ScoreLabel");
        _configButton = GetNode<Button>("ConfigButton");
        _configMenu = GetNode<ConfigMenu>("ConfigMenu");
        _jumpSound = GetNode<AudioStreamPlayer>("JumpSound");
        _landSound = GetNode<AudioStreamPlayer>("LandSound");
        _hitSound = GetNode<AudioStreamPlayer>("HitSound");
    }

    private void ConnectUi()
    {
        _configButton!.Pressed += () => SetConfigOpen(true);
        _configMenu!.Closed += () => SetConfigOpen(false);
    }

    private void ConnectMonitors()
    {
        _gameplay!.OnChange(_ => CallDeferred(MethodName.RefreshUi));
    }

    private void RefreshUi()
    {
        if (_scoreLabel is null)
        {
            return;
        }

        _scoreLabel!.Text = $"Score {_score}";
    }

    private void ResetArena()
    {
        _playerPosition = new Vector2(GetViewportRect().Size.X * 0.5f, GetGroundY());
        _playerVelocityY = 0;
        _grounded = true;
        _jumpWasPressed = false;
        _spawnTimer = 0.6f;
        _enemies.Clear();
        _explosions.Clear();
        _damageNumbers.Clear();
    }

    private void UpdateEnemies(float seconds)
    {
        var size = GetViewportRect().Size;
        var gameplay = _gameplay!.CurrentValue;
        var difficulty = GetDifficulty(gameplay.Difficulty);
        var spawnInterval = Mathf.Max(0.35f, difficulty.SpawnInterval / Mathf.Max(0.1f, gameplay.SpawnRate));

        _spawnTimer -= seconds;
        if (_spawnTimer <= 0)
        {
            SpawnEnemy(difficulty);
            _spawnTimer = spawnInterval;
        }

        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            var direction = Math.Sign(_playerPosition.X - enemy.Position.X);
            if (direction == 0)
            {
                direction = 1;
            }

            enemy.Position = new Vector2(enemy.Position.X + direction * difficulty.Speed * seconds, GetGroundY());
            if (Mathf.Abs(enemy.Position.X - _playerPosition.X) < PlayerSize)
            {
                _enemies.RemoveAt(i);
                _score = Math.Max(0, _score - 1);
                PlayTone(_hitSound, 160f, 0.08f, 0.16f);
                RefreshUi();
                continue;
            }

            if (enemy.Position.X < -80 || enemy.Position.X > size.X + 80)
            {
                _enemies.RemoveAt(i);
            }
        }
    }

    private void UpdateDamageNumbers(float seconds)
    {
        for (var i = _damageNumbers.Count - 1; i >= 0; i--)
        {
            var damageNumber = _damageNumbers[i];
            damageNumber.TimeLeft -= seconds;
            damageNumber.Position += new Vector2(0, -58f * seconds);

            if (damageNumber.TimeLeft <= 0)
            {
                _damageNumbers.RemoveAt(i);
            }
        }
    }

    private void UpdateExplosions(float seconds)
    {
        for (var i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].TimeLeft -= seconds;
            if (_explosions[i].TimeLeft <= 0)
            {
                _explosions.RemoveAt(i);
            }
        }
    }

    private void SpawnEnemy(DifficultySettings difficulty)
    {
        var size = GetViewportRect().Size;
        var fromLeft = _random.RandiRange(0, 1) == 0;
        _enemies.Add(new Enemy
        {
            Position = new Vector2(fromLeft ? -24 : size.X + 24, GetGroundY()),
            Size = difficulty.EnemySize,
        });
    }

    private void Land()
    {
        var killed = 0;
        var showDamageNumbers = _gameplay!.CurrentValue.ShowDamageNumbers;
        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i].Position.DistanceTo(_playerPosition) <= SplashRadius)
            {
                if (showDamageNumbers)
                {
                    _damageNumbers.Add(new DamageNumber
                    {
                        Position = _enemies[i].Position + new Vector2(_random.RandfRange(-16f, 16f), -44f),
                        Text = "999",
                        TimeLeft = DamageNumberDuration,
                    });
                }

                _enemies.RemoveAt(i);
                killed++;
            }
        }

        _explosions.Add(new Explosion
        {
            Position = _playerPosition,
            TimeLeft = ExplosionDuration,
        });
        PlayTone(_landSound, 95f, 0.13f, 0.2f);

        if (killed > 0)
        {
            _score += killed;
            PlayTone(_hitSound, 260f, 0.16f, 0.24f);
            RefreshUi();
        }
    }

    private void SetConfigOpen(bool open)
    {
        _configMenu!.Visible = open;
        _configButton!.Visible = !open;
    }

    private float GetGroundY() => GetViewportRect().Size.Y - GroundHeight;

    private static DifficultySettings GetDifficulty(string difficulty)
        => difficulty switch
        {
            "Easy" => new DifficultySettings(115f, 1.25f, 28f),
            "Hard" => new DifficultySettings(205f, 0.8f, 34f),
            _ => new DifficultySettings(155f, 1.0f, 31f),
        };

    private static void PlayTone(AudioStreamPlayer? player, float frequency, float duration, float volume)
    {
        if (player?.Stream is not AudioStreamGenerator generator)
        {
            return;
        }

        player.Stop();
        player.Play();

        if (player.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
        {
            return;
        }

        var frameCount = Math.Max(1, (int)(ToneSampleRate * duration));
        for (var i = 0; i < frameCount; i++)
        {
            var progress = i / (float)frameCount;
            var envelope = 1f - progress;
            var sample = Mathf.Sin(Mathf.Tau * frequency * i / generator.MixRate) * volume * envelope;
            playback.PushFrame(new Vector2(sample, sample));
        }
    }

    private sealed class Enemy
    {
        public Vector2 Position { get; set; }

        public float Size { get; set; }
    }

    private sealed class Explosion
    {
        public Vector2 Position { get; set; }

        public float TimeLeft { get; set; }
    }

    private sealed class DamageNumber
    {
        public Vector2 Position { get; set; }

        public string Text { get; init; } = string.Empty;

        public float TimeLeft { get; set; }
    }

    private readonly record struct DifficultySettings(float Speed, float SpawnInterval, float EnemySize);
}

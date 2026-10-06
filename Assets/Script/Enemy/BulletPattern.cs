using System.Collections;
using UnityEngine;

namespace KazumaPrototype
{
    // ==============================================
    // ボスが使用する6種類の弾幕パターンを管理する。
    // KazumaPrototypeArenaから発射命令を受け、
    // 実際の弾生成はArenaのSpawnBulletを使用する。
    // ==============================================
    public sealed class BulletPattern : MonoBehaviour
    {
        // ============================================================
        // Inspector設定
        // ============================================================

        [Header("参照")]
        [Tooltip("弾を生成するKazumaPrototypeArena")]
        [SerializeField] private KazumaPrototypeArena arena;

        [Tooltip("弾幕の発射基準位置")]
        [SerializeField] private Transform firePoint;

        [Header("基本速度")]
        [Tooltip("敵弾の基本移動速度")]
        [SerializeField] private float bulletSpeed = 4.0f;

        [Header("連続発射")]
        [Tooltip("螺旋やカーテンなどの連続発射間隔")]
        [SerializeField] private float rapidFireInterval = 0.10f;

        [Header("各パターンの発射継続時間")]
        [Tooltip("Pattern1が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern1Duration = 1.0f;

        [Tooltip("Pattern2が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern2Duration = 1.0f;

        [Tooltip("Pattern3が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern3Duration = 1.5f;

        [Tooltip("Pattern4が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern4Duration = 1.5f;

        [Tooltip("Pattern5が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern5Duration = 2.0f;

        [Tooltip("Pattern6が弾を出し続ける時間")]
        [SerializeField, Min(0.1f)]
        private float pattern6Duration = 2.0f;

        [Header("各パターンの弾生成間隔")]
        [Tooltip("Pattern1の1セットごとの発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern1FireInterval = 0.35f;

        [Tooltip("Pattern2の1セットごとの発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern2FireInterval = 0.35f;

        [Tooltip("Pattern3の発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern3FireInterval = 0.10f;

        [Tooltip("Pattern4の1セットごとの発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern4FireInterval = 0.35f;

        [Tooltip("Pattern5の1セットごとの発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern5FireInterval = 0.50f;

        [Tooltip("Pattern6の横一列ごとの発射間隔")]
        [SerializeField, Min(0.02f)]
        private float pattern6FireInterval = 0.35f;

        // ============================================================
        // 現在実行中の弾幕数
        // ============================================================
        // Patternを同時発動できるように、
        // boolではなく「現在何個のCoroutineが動いているか」で管理する。
        //
        // 例：
        // Pattern1だけ実行中       → 1
        // Pattern1 + Pattern4実行中 → 2
        // 全Pattern終了            → 0
        // ============================================================
        private int activePatternCount = 0;

        // 1個以上のPatternが動いていればtrue。
        public bool IsFiring => activePatternCount > 0;

        // ============================================================
        // 弾幕選択
        // ============================================================

        // 6種類の弾幕からランダムで1種類を発射する。
        public void FireRandomPattern()
        {
            if (IsFiring)
            {
                return;
            }

            int patternNumber = Random.Range(1, 7);

            FirePattern(patternNumber);
        }

        // ============================================================
        // 指定されたPatternを発射
        // ============================================================
        // 複数Patternの同時発動に対応しているため、
        // 他のPatternが実行中でも新しいPatternを開始できる。
        // ============================================================
        public void FirePattern(int patternNumber)
        {
            switch (patternNumber)
            {
                case 1:
                    StartCoroutine(
                        RunPattern(FireGridPattern()));
                    break;

                case 2:
                    StartCoroutine(
                        RunPattern(FireRadialPattern()));
                    break;

                case 3:
                    StartCoroutine(
                        RunPattern(FireSpiralPattern()));
                    break;

                case 4:
                    StartCoroutine(
                        RunPattern(FireFanPattern()));
                    break;

                case 5:
                    StartCoroutine(
                        RunPattern(FireFlowerPattern()));
                    break;

                case 6:
                    StartCoroutine(
                        RunPattern(FireCurtainPattern()));
                    break;

                default:
                    Debug.LogWarning(
                        $"存在しない弾幕番号 : {patternNumber}");
                    break;
            }
        }

        // ============================================================
        // Pattern Coroutine共通管理
        // ============================================================
        // Pattern開始時にactivePatternCountを増やし、
        // 終了時に減らす。
        //
        // これによってArena側はIsFiringを見るだけで
        // 「同時発動したPatternが全部終了したか」
        // を判断できる。
        // ============================================================
        private IEnumerator RunPattern(
            IEnumerator patternRoutine)
        {
            activePatternCount++;

            // 指定された弾幕Coroutineを最後まで実行。
            yield return StartCoroutine(patternRoutine);

            activePatternCount--;

            // 念のため0未満にならないようにする。
            activePatternCount =
                Mathf.Max(0, activePatternCount);
        }
        // ============================================================
        // Pattern 1：格子
        // 横方向へ並んだ弾を複数段生成する。
        // 中央部分には縦方向の逃げ道を作る
        // ============================================================
        private IEnumerator FireGridPattern()
        {

            const int RowCount = 2;
            const int ColumnCount = 7;

            const float HorizontalSpacing = 0.85f;
            const float RowSpacing = 1.1f;

            float elapsedTime = 0.0f;

            while (elapsedTime < pattern1Duration)
            {
                Vector3 startPosition =
                    firePoint.position + Vector3.back * 2.5f;

                for (int row = 0; row < RowCount; row++)
                {
                    for (int column = 0; column < ColumnCount; column++)
                    {
                        // 中央列は逃げ道として空ける
                        if (column == ColumnCount / 2)
                        {
                            continue;
                        }

                        float x =
                            (column - (ColumnCount - 1) * 0.5f)
                            * HorizontalSpacing;

                        Vector3 position =
                            startPosition
                            + Vector3.right * x
                            + Vector3.back * (row * RowSpacing);

                        int power =
                            3 - (column % 3);

                        SpawnDownwardBullet(
                            position,
                            power);
                    }
                }

                yield return new WaitForSeconds(
                    pattern1FireInterval);

                elapsedTime += pattern1FireInterval;
            }

        }

        // ============================================================
        // Pattern 2：放射
        // ボスを中心として、
        // 左下・中央下・右下の3方向へ弾を放射する。
        // ============================================================
        private IEnumerator FireRadialPattern()
        {

            const int BulletPerDirection = 6;

            float[] baseAngles =
            {
                 -45.0f,
                   0.0f,
                  45.0f
            };

            float elapsedTime = 0.0f;

            while (elapsedTime < pattern2Duration)
            {
                for (int directionIndex = 0;
                     directionIndex < baseAngles.Length;
                     directionIndex++)
                {
                    for (int bulletIndex = 0;
                         bulletIndex < BulletPerDirection;
                         bulletIndex++)
                    {
                        float spread =
                            (bulletIndex - (BulletPerDirection - 1) * 0.5f)
                            * 4.0f;

                        float angle =
                            baseAngles[directionIndex] + spread;

                        int power =
                            1 + bulletIndex % 3;

                        SpawnAngleBullet(
                            firePoint.position,
                            angle,
                            power);
                    }
                }

                yield return new WaitForSeconds(
                    pattern2FireInterval);

                elapsedTime += pattern2FireInterval;
            }

        }

        // ============================================================
        // Pattern 3：螺旋
        // 発射角度を少しずつ回転させながら撃つことで
        // 螺旋状の弾幕を作る。
        // ============================================================
        private IEnumerator FireSpiralPattern()
        {
            
            const float AngleStep = 17.0f;

            float currentAngle = 0.0f;
            float elapsedTime = 0.0f;
            int shot = 0;

            while (elapsedTime < pattern3Duration)
            {
                int power =
                    1 + shot % 3;

                SpawnAngleBullet(
                    firePoint.position,
                    currentAngle,
                    power);

                // 反対方向にも発射
                SpawnAngleBullet(
                    firePoint.position,
                    currentAngle + 180.0f,
                    power);

                currentAngle += AngleStep;

                shot++;

                yield return new WaitForSeconds(
                    pattern3FireInterval);

                elapsedTime += pattern3FireInterval;
            }

        }

        // ============================================================
        // Pattern 4：扇形
        // ボスから下方向へ広がる扇状の弾幕。
        // 外側から内側へLv3 → Lv2 → Lv1を配置する。
        // ============================================================
        private IEnumerator FireFanPattern()
        {
            
            const int BulletCount = 13;
            const float TotalAngle = 100.0f;

            float elapsedTime = 0.0f;

            while (elapsedTime < pattern4Duration)
            {
                for (int index = 0;
                     index < BulletCount;
                     index++)
                {
                    float rate =
                        index / (float)(BulletCount - 1);

                    float angle =
                        Mathf.Lerp(
                            -TotalAngle * 0.5f,
                            TotalAngle * 0.5f,
                            rate);

                    float centerDistance =
                        Mathf.Abs(
                            index -
                            (BulletCount - 1) * 0.5f);

                    int power;

                    if (centerDistance >= 4.0f)
                    {
                        power = 3;
                    }
                    else if (centerDistance >= 2.0f)
                    {
                        power = 2;
                    }
                    else
                    {
                        power = 1;
                    }

                    SpawnAngleBullet(
                        firePoint.position,
                        angle,
                        power);
                }

                yield return new WaitForSeconds(
                    pattern4FireInterval);

                elapsedTime += pattern4FireInterval;
            }

        }

        // ============================================================
        // Pattern 5：花
        // 3つの円形弾幕を重ねて花のような形を作る。
        // 外側Lv3、中間Lv2、内側Lv1。
        // ============================================================
        private IEnumerator FireFlowerPattern()
        {
            
            float elapsedTime = 0.0f;

            while (elapsedTime < pattern5Duration)
            {
                SpawnRing(
                    firePoint.position,
                    16,
                    3,
                    bulletSpeed * 0.85f,
                    0.0f);

                yield return new WaitForSeconds(0.12f);

                elapsedTime += 0.12f;

                if (elapsedTime >= pattern5Duration)
                {
                    break;
                }

                SpawnRing(
                    firePoint.position,
                    12,
                    2,
                    bulletSpeed,
                    15.0f);

                yield return new WaitForSeconds(0.12f);

                elapsedTime += 0.12f;

                if (elapsedTime >= pattern5Duration)
                {
                    break;
                }

                SpawnRing(
                    firePoint.position,
                    8,
                    1,
                    bulletSpeed * 1.15f,
                    0.0f);

                float waitTime =
                    Mathf.Max(
                        0.02f,
                        pattern5FireInterval);

                yield return new WaitForSeconds(waitTime);

                elapsedTime += waitTime;
            }

        }

        // ============================================================
        // Pattern 6：カーテン
        // 横一列の弾を画面上部に並べ、
        // カーテンのように下方向へ流す。
        // ============================================================
        private IEnumerator FireCurtainPattern()
        {
            
            const int BulletCount = 13;
            const float HorizontalSpacing = 0.65f;

            float elapsedTime = 0.0f;
            int wave = 0;

            while (elapsedTime < pattern6Duration)
            {
                for (int index = 0;
                     index < BulletCount;
                     index++)
                {
                    float x =
                        (index - (BulletCount - 1) * 0.5f)
                        * HorizontalSpacing;

                    Vector3 position =
                        firePoint.position
                        + Vector3.right * x
                        + Vector3.back * 1.5f;

                    int power;

                    switch ((index + wave) % 3)
                    {
                        case 0:
                            power = 3;
                            break;

                        case 1:
                            power = 2;
                            break;

                        default:
                            power = 1;
                            break;
                    }

                    SpawnDownwardBullet(
                        position,
                        power);
                }

                wave++;

                yield return new WaitForSeconds(
                    pattern6FireInterval);

                elapsedTime += pattern6FireInterval;
            }

        }

        // ============================================================
        // 共通：下方向へ発射
        // ============================================================
        private void SpawnDownwardBullet(
            Vector3 position,
            int power)
        {
            Vector3 Velocity =
                Vector3.back * bulletSpeed;

            arena.SpawnBullet(
                position,
                Velocity,
                power);
        }

        // ============================================================
        // 共通：角度指定発射
        // 下方向を0度として、
        // 指定した角度へ弾を発射する。
        // ============================================================
        private void SpawnAngleBullet(
            Vector3 position,
            float angle,
            int power)
        {
            Vector3 Direction =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up)
                * Vector3.back;

            arena.SpawnBullet(
                position,
                Direction * bulletSpeed,
                power);
        }

        // ============================================================
        // 共通：円形発射
        // ============================================================
        private void SpawnRing(
            Vector3 position,
            int bulletCount,
            int power,
            float speed,
            float angleOffset)
        {
            float AngleStep =
                360.0f / bulletCount;

            for (int Index = 0;
                 Index < bulletCount;
                 Index++)
            {
                float Angle =
                    Index * AngleStep
                    + angleOffset;

                Vector3 Direction =
                    Quaternion.AngleAxis(
                        Angle,
                        Vector3.up)
                    * Vector3.back;

                arena.SpawnBullet(
                    position,
                    Direction * speed,
                    power);
            }
        }
    }
}
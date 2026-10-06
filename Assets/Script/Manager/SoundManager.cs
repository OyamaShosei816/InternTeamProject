using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // BGM用
    [System.Serializable]           // ←Inspectorから値がセットできるように
    public class BGMData
    {
        public string name;
        public AudioClip audioClip;
        public float volume = 1.0f;
        public bool isLoop;
    }
    // SE用
    [System.Serializable]
    public class SEData
    {
        public string name;
        public AudioClip audioClip;
        public float volume = 1.0f;

        // 追加
        public bool isLoop;
    }

    [Header("BGM設定")]
    [SerializeField] private BGMData[] bgmData;

    [Header("SE設定")]
    [SerializeField] private SEData[] seData;


    // 同時に鳴らしたいSEの数
    private AudioSource[] seAudioSourceList = new AudioSource[10];
    // BGM専用のAudioSource
    private AudioSource bgmAudioSource;

    // 別名（name）をキーとした管理用Dictionary
    private Dictionary<string, BGMData> bgmDictionary = new Dictionary<string, BGMData>();
    private Dictionary<string, SEData> seDictionary = new Dictionary<string, SEData>();

    // 再生中のサウンドデータを保持
    private BGMData currentPlayingBGM = null;


    /////////////////////////
    //  Awake
    /////////////////////////
    private void Awake()
    {
        // =====================================================
        // Singleton
        // =====================================================

        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;


        // =====================================================
        // Sceneを跨いでも残す
        // =====================================================

        DontDestroyOnLoad(gameObject);


        // =====================================================
        // BGM用AudioSource
        // =====================================================

        bgmAudioSource =
            gameObject.AddComponent<AudioSource>();


        // =====================================================
        // SE用AudioSource
        // =====================================================

        for (int i = 0;
             i < seAudioSourceList.Length;
             ++i)
        {
            seAudioSourceList[i] =
                gameObject.AddComponent<AudioSource>();
        }


        // =====================================================
        // BGM Dictionary
        // =====================================================

        foreach (var data in bgmData)
        {
            if (data == null ||
                string.IsNullOrEmpty(data.name))
            {
                continue;
            }

            if (!bgmDictionary.ContainsKey(
                    data.name))
            {
                bgmDictionary.Add(
                    data.name,
                    data
                );
            }
        }

        // =====================================================
        // SE Dictionary
        // =====================================================
        foreach (var data in seData)
        {
            if (data == null ||
                string.IsNullOrEmpty(data.name))
            {
                continue;
            }

            if (!seDictionary.ContainsKey(
                    data.name))
            {
                seDictionary.Add(
                    data.name,
                    data
                );
            }
        }
    }

    /////////////////////////
    //  Update
    /////////////////////////
    private void Update()
    {
        // BGMが再生中、インスペクターで音量を変更できるようにする
        if (bgmAudioSource != null &&
            bgmAudioSource.isPlaying &&
            currentPlayingBGM != null)
        {
            bgmAudioSource.volume = Mathf.Clamp01(currentPlayingBGM.volume);
        }
        else if (bgmAudioSource != null && !bgmAudioSource.isPlaying)
        {
            currentPlayingBGM = null;
        }
    }

    // -- GetUnusedSEAudioSource --
    // 未使用のSE用AudioSourceの取得
    private AudioSource GetUnusedSEAudioSource()
    {
        for (var i = 0; i < seAudioSourceList.Length; ++i)
        {
            if (seAudioSourceList[i].isPlaying == false)
            {
                return seAudioSourceList[i];
            }
        }

        return null; // 未使用のAudioSourceがない場合はnullを返す
    }

    ///////////////////////////////////////////////
    // PlaySE
    // 指定されたSEを再生
    ///////////////////////////////////////////////
    public void PlaySE(string name)
    {
        if (!seDictionary.TryGetValue(
                name,
                out var soundData))
        {
            Debug.Log(
                $"<color=cyan>登録されていません:{name}</color>"
            );

            return;
        }


        // =====================================================
        // 同じLoopSEがすでに鳴っていたら二重再生しない
        // =====================================================

        if (soundData.isLoop)
        {
            foreach (AudioSource source
                     in seAudioSourceList)
            {
                if (source == null)
                {
                    continue;
                }

                if (source.isPlaying &&
                    source.loop &&
                    source.clip ==
                    soundData.audioClip)
                {
                    return;
                }
            }
        }


        // =====================================================
        // 空いているAudioSource取得
        // =====================================================

        AudioSource audioSource =
            GetUnusedSEAudioSource();

        if (audioSource == null)
        {
            Debug.LogWarning(
                "[SoundManager] 使用可能なSE AudioSourceがありません"
            );

            return;
        }


        // =====================================================
        // SE設定
        // =====================================================

        audioSource.Stop();

        audioSource.clip =
            soundData.audioClip;

        audioSource.loop =
            soundData.isLoop;

        audioSource.volume =
            Mathf.Clamp01(
                soundData.volume
            );

        audioSource.time =
            0f;

        // =====================================================
        // 再生
        // =====================================================
        audioSource.Play();
    }
    ///////////////////////////////////////////////
    //  PlaySEWithDelay
    //     ディレイを作ってSEを再生
    ///////////////////////////////////////////////
    public void PlaySEWithDelay(string name, float delaySeconds)
    {
        IEnumerator PlaySEAfterDelayRoutine()
        {
            yield return new WaitForSeconds(delaySeconds);
            PlaySE(name);
        }

        StartCoroutine(PlaySEAfterDelayRoutine());
    }


    ///////////////////////////////////////////////
    //  PlayBGM
    //     指定された別名で登録されたBGMを再生
    ///////////////////////////////////////////////
    public void PlayBGM(string name)
    {
        // 既に同じBGMが再生中なら何もしない
        if (bgmDictionary.TryGetValue(name, out var soundData))
        {
            // 既に同じBGMが再生中なら何もしない
            if (bgmAudioSource.isPlaying &&
                bgmAudioSource.clip == soundData.audioClip)
            {
                return;
            }

            currentPlayingBGM = soundData;

            bgmAudioSource.loop = soundData.isLoop;
            bgmAudioSource.clip = soundData.audioClip;

            bgmAudioSource.volume = Mathf.Clamp01(soundData.volume);

            bgmAudioSource.Play();
        }
        else
        {
            Debug.Log($"<color=cyan>登録されていません:{name}</color>");
        }
    }

    ///////////////////////////////////////////////
    // PlaySEForDuration
    // 指定したSEを指定秒数だけ再生
    ///////////////////////////////////////////////
    public void PlaySEForDuration(
        string name,
        float duration)
    {
        if (!seDictionary.TryGetValue(
            name,
            out var soundData))
        {
            Debug.Log(
                $"<color=cyan>登録されていません:{name}</color>"
            );

            return;
        }

        AudioSource audioSource =
            GetUnusedSEAudioSource();

        if (audioSource == null)
        {
            return;
        }

        audioSource.loop = false;

        audioSource.clip =
            soundData.audioClip;

        audioSource.volume =
            Mathf.Clamp01(
                soundData.volume
            );

        audioSource.time = 0f;

        audioSource.Play();

        StartCoroutine(
            StopSEAfterDuration(
                audioSource,
                duration
            )
        );
    }
    private IEnumerator StopSEAfterDuration(
    AudioSource audioSource,
    float duration)
    {
        yield return new WaitForSecondsRealtime(
            duration
        );

        // 同じAudioSourceがまだ同じSEを再生している場合のみ停止
        if (audioSource != null &&
            audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    ///////////////////////////////////////////////
    // PlaySEFromTime
    // SEの指定秒数地点から再生
    //
    // 例：
    // PlaySEFromTime("GameOverSE", 2.3f);
    // → 音源の0～2.3秒を飛ばし、
    //    2.3秒地点を先頭として即再生する
    ///////////////////////////////////////////////
    public void PlaySEFromTime(
        string name,
        float startTime)
    {
        // SEを名前から検索
        if (!seDictionary.TryGetValue(
            name,
            out var soundData))
        {
            Debug.Log(
                $"<color=cyan>登録されていません:{name}</color>"
            );

            return;
        }

        // 使用可能なAudioSourceを取得
        AudioSource audioSource =
            GetUnusedSEAudioSource();

        if (audioSource == null)
        {
            return;
        }

        AudioClip clip =
            soundData.audioClip;

        if (clip == null)
        {
            Debug.LogWarning(
                "[SoundManager] AudioClipがありません : " +
                name
            );

            return;
        }

        // =========================================
        // 再生開始位置を安全な範囲にする
        // =========================================
        float safeStartTime =
            Mathf.Clamp(
                startTime,
                0f,
                Mathf.Max(0f, clip.length - 0.01f)
            );

        // =========================================
        // AudioSource設定
        // =========================================
        audioSource.loop = false;

        audioSource.clip =
            clip;

        audioSource.volume =
            Mathf.Clamp01(
                soundData.volume
            );

        // =========================================
        // 音源内の再生開始位置
        // =========================================
        audioSource.time =
            safeStartTime;

        // =========================================
        // 即再生
        // =========================================
        audioSource.Play();
    }

    /////////////////////
    //  StopBGM
    //     BGM停止
    /////////////////////
    public void StopBGM()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = null;
            currentPlayingBGM = null;
        }
    }

    ///////////////////////////////////////////////
    // StopSE
    // 指定した名前のSEを停止
    ///////////////////////////////////////////////
    public void StopSE(string name)
    {
        // =========================================
        // SEデータを名前から取得
        // =========================================
        if (!seDictionary.TryGetValue(
            name,
            out var soundData))
        {
            Debug.LogWarning(
                "[SoundManager] 登録されていないSEです : " +
                name
            );

            return;
        }

        // =========================================
        // このSEを再生しているAudioSourceを探す
        // =========================================
        foreach (AudioSource audioSource
                 in seAudioSourceList)
        {
            if (audioSource == null)
            {
                continue;
            }

            // 同じAudioClipなら停止
            if (audioSource.clip ==
                soundData.audioClip)
            {
                audioSource.Stop();

                audioSource.clip = null;

                audioSource.loop = false;

                audioSource.time = 0f;
            }
        }
    }
}

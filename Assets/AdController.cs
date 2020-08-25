using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.Monetization;

public class AdController : MonoBehaviour
{

    public static AdController instance;

    private string store_id = "3757777";
    private string video_ad_id = "video";
    private string video_rewarded_id = "rewardedVideo";
    // Start is called before the first frame update

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    void Start()
    {
        Advertisement.Initialize(store_id, false);
    }

    // Update is called once per frame
    void Update()
    {
       /* if (Input.GetKeyDown(KeyCode.E))
        {
            if (Monetization.IsReady(video_ad_id))
            {
                ShowAdPlacementContent ad = null;
                ad = Monetization.GetPlacementContent(video_ad_id) as ShowAdPlacementContent;
                if (ad != null)
                {
                    ad.Show();
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (Monetization.IsReady(video_ad_id))
            {
                ShowAdPlacementContent ad = null;
                ad = Monetization.GetPlacementContent(video_rewarded_id) as ShowAdPlacementContent;
                if (ad != null)
                {
                    ad.Show();
                }
            }
        }*/
    }
    public void showInterstitial()
    {
        if (Advertisement.IsReady(video_ad_id))
        {
            Advertisement.Show(video_ad_id);
        }
    }
}

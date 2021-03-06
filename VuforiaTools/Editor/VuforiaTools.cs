using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class VuforiaTools : MonoBehaviour
{
    public Texture2D texture;
    private VuforiaToolsConfiguration vtc;

    //Server keys are placed here
    private string access_key = "";
    private string secret_key = "";
    //Address of Vuforia's server
    private string url = @"https://vws.vuforia.com";

    public VuforiaTools()
    {
        vtc = AssetDatabase.LoadAssetAtPath("Assets/Resources/VuforiaToolsConfiguration.asset", typeof(VuforiaToolsConfiguration)) as VuforiaToolsConfiguration;
        access_key = vtc.accessKey;
        secret_key = vtc.secretKey;
    }

    //Function to upload the image target
    public string UploadImageTarget(string targetName, float width, string imagePath, bool active, string metaData)
    {
        //Gets all target names, sorts them, then checks to see if the name you're trying to upload is already taken.
        List<string> sortedNames = new List<string>();
        foreach(VtTargetSummary summary in vtc.targetSummaryList){
            sortedNames.Add(summary.target_name);
        }
        if (sortedNames.Contains(targetName.ToLower()))
        {
            Debug.Log("That target name already exists pick another one!");
            return "nameError";
        }
        else
        {
            bool returnThing = UploadTarget(targetName, width, imagePath, active, metaData);
            if (returnThing)
            {
                return "true";
            }
            else
            {
                return "false";
            }
        }
    }

    public string UpdateImageTarget(string targetID, string targetName, float width, string imagePath, bool active, string metaData)
    {
        string returnThing = UpdateTarget(targetID, targetName, width, imagePath, active, metaData);
        return returnThing;
    }

    public List<string> GetAllTargetNames()
    {
        vtc.targetSummaryList = new List<VtTargetSummary>();
        string requestPath = "/targets";
        string serviceURI = url + requestPath;
        string httpAction = "GET";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest webRequest = UnityWebRequest.Get(serviceURI);
        string requestString = VuforiaRequest(requestPath, httpAction, contentType, requestBody, webRequest);

        List<string> stringToReturn = GetTargetIDsFromString(requestString);

        List<string> characterList = new List<string>();
        for (int i = 0; i < stringToReturn.Count; i++)
        {
            //Makes a query to the server for each target ID to get the target name
            //then adds the target names to the arraylist

            string targetSumString = GetTargetSummary((string)stringToReturn[i]);
            VtTargetSummary targetSummary = JsonUtility.FromJson<VtTargetSummary>(targetSumString);
            targetSummary.target_id = (string)stringToReturn[i];
            string dateSplit = targetSumString.Split(new string[] { @"""upload_date"":""" }, StringSplitOptions.None)[1].Remove(10);
            string[] dateYYYYMMDD = dateSplit.Split('-');
            targetSummary.upload_date = dateYYYYMMDD[1] + "/" + dateYYYYMMDD[2] + "/" + dateYYYYMMDD[0];
            vtc.targetSummaryList.Add(targetSummary);
            characterList.Add(targetSummary.target_name);

            if (characterList.Count > 0)
            {
                float percentComplete = ((float)characterList.Count / stringToReturn.Count);
                EditorUtility.DisplayProgressBar("Populating Target Information", characterList.Count + "/" + stringToReturn.Count + ": " + targetSummary.target_name, percentComplete);
            }

        }
        if (characterList.Count > 0)
        {
            EditorUtility.ClearProgressBar();
        }

        string[] namesList = AssetDatabase.GetAllAssetBundleNames();
        List<string> sortedNames = new List<string>();

        foreach (string name1 in characterList)
        {
            sortedNames.Add(name1);
        }

        return sortedNames;
    }

    private bool UploadTarget(string targetName, float width, string imagePath, bool active, string metaData)
    {
        string requestPath = "/targets";
        string serviceURI = url + requestPath;
        string httpAction = "POST";
        string contentType = "application/json";

        byte[] imgBytes = File.ReadAllBytes(imagePath);
        string imageEncode64 = Convert.ToBase64String(imgBytes);
        JsonPost targetPost = new JsonPost(targetName, width, imageEncode64, active, metaData);
        string requestBody = JsonUtility.ToJson(targetPost);

        //Post request requires string input, but the requestBody will cause a 401 unauthorized error, 
        //so you need to use kHttpVerbPost, then set the upload handler separately
        UnityWebRequest webRequest = UnityWebRequest.Post(serviceURI, UnityWebRequest.kHttpVerbPOST);
        UploadHandlerRaw MyUploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(requestBody));
        webRequest.uploadHandler = MyUploadHandler;

        string returnString = VuforiaRequest(requestPath, httpAction, contentType, requestBody, webRequest);

        VtTargetSummary newItem = new VtTargetSummary();
        VtUploadResultCode code = JsonUtility.FromJson<VtUploadResultCode>(returnString);
        newItem.target_id = code.target_id;
        vtc.targetSummaryList.Add(newItem);
        UpdateTargetInformation(code.target_id);
        if(returnString == "fail")
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public void UpdateTargetInformation(string target_id){
        int indexNumber = vtc.targetSummaryList.FindIndex(item => item.target_id == target_id);
        if(indexNumber>-1){
            vtc.targetSummaryList.RemoveAt(indexNumber);
        }

        string targetSumString = GetTargetSummary(target_id);
        if(targetSumString == "fail"){
            VtTargetSummary blank = new VtTargetSummary();
            blank.target_id = target_id;
            blank.target_name = "New Target, Try Refresh";
            blank.upload_date = DateTime.Now.Date.ToString();
        }
        else{
            VtTargetSummary targetSummary = JsonUtility.FromJson<VtTargetSummary>(targetSumString);
            targetSummary.target_id = target_id;
            string dateSplit = targetSumString.Split(new string[] { @"""upload_date"":""" }, StringSplitOptions.None)[1].Remove(10);
            string[] dateYYYYMMDD = dateSplit.Split('-');
            targetSummary.upload_date = dateYYYYMMDD[1] + "/" + dateYYYYMMDD[2] + "/" + dateYYYYMMDD[0];
            vtc.targetSummaryList.Insert(indexNumber, targetSummary);
        }
    }

    private string UpdateTarget(string targetID, string targetName, float width, string imagePath, bool active, string metaData)
    {
        string requestPath = "/targets/" + targetID;
        string serviceURI = url + requestPath;
        string httpAction = "PUT";
        string contentType = "application/json";

        //Upload target as json. Image needs to be base64 string
        string imageEncode64 = null;
        if (imagePath != null)
        {
            byte[] imgBytes = File.ReadAllBytes(imagePath);
            imageEncode64 = Convert.ToBase64String(imgBytes);
        }
        JsonPost targetPost = new JsonPost(targetName, width, imageEncode64, active, metaData);
        //The object being posted, but also the body that forms the request.
        string requestBody = "{" + (targetName != null ? @"""name"":""" + targetName + @"""," : "") + @"""width"":" + width + 
            (imageEncode64 != null ? @",""image"":""" + imageEncode64 + @""",""active_flag"":" : @",""active_flag"":") + active.ToString().ToLower() + 
            (metaData != null ? @",""application_metadata"":""" + metaData + @"""}" : "}");

        UnityWebRequest webRequest = UnityWebRequest.Put(serviceURI, System.Text.Encoding.UTF8.GetBytes(requestBody));
        string returnString = VuforiaRequest(requestPath, httpAction, contentType, requestBody, webRequest);
        UpdateTargetInformation(targetID);
        return returnString;
    }

    public List<string> GetTargetIDsFromString(string readString)
    {
        TargetList targetList = JsonUtility.FromJson<TargetList>(readString);
        return targetList.results;
    }

    public string ReturnCharacterName(string targetID)
    {
        string requestPath = "/targets/" + targetID;
        string serviceURI = url + requestPath;
        string httpAction = "GET";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest webRequest = UnityWebRequest.Get(serviceURI);
        return VuforiaRequest(requestPath, httpAction, contentType, requestBody, webRequest);
    }

    public string CheckDuplicates(string targetID)
    {
        string requestPath = "/duplicates/" + targetID;
        string serviceURI = url + requestPath;
        string httpAction = "GET";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest unityWebRequest = UnityWebRequest.Get(serviceURI);
        return VuforiaRequest(requestPath, httpAction, contentType, requestBody, unityWebRequest);
    }

    public string GetAccountSummary()
    {
        string requestPath = "/summary";
        string serviceURI = url + requestPath;
        string httpAction = "GET";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest unityWebRequest = UnityWebRequest.Get(serviceURI);
        return VuforiaRequest(requestPath, httpAction, contentType, requestBody, unityWebRequest);
    }

    public string GetTargetSummary(string targetID)
    {
        string requestPath = "/summary/" + targetID;
        string serviceURI = url + requestPath;
        string httpAction = "GET";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest unityWebRequest = UnityWebRequest.Get(serviceURI);
        return VuforiaRequest(requestPath, httpAction, contentType, requestBody, unityWebRequest);
    }

    public string DeleteTarget(string targetID)
    {
        string requestPath = "/targets/" + targetID;
        string serviceURI = url + requestPath;
        string httpAction = "DELETE";
        string contentType = "";
        string requestBody = "";

        UnityWebRequest unityWebRequest = UnityWebRequest.Delete(serviceURI);
        string returnString = VuforiaRequest(requestPath, httpAction, contentType, requestBody, unityWebRequest);
        UpdateTargetInformation(targetID);
        return returnString;
        
    }

    public string VuforiaRequest(string requestPath, string httpAction, string contentType, string requestBody, UnityWebRequest unityWebRequest)
    {
        string serviceURI = url + requestPath;
        string date = string.Format("{0:r}", DateTime.Now.ToUniversalTime());

        unityWebRequest.SetRequestHeader("host", url);
        unityWebRequest.SetRequestHeader("date", date);
        unityWebRequest.SetRequestHeader("content-type", contentType);

        MD5 md5 = MD5.Create();
        var contentMD5bytes = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(requestBody));
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < contentMD5bytes.Length; i++)
        {
            sb.Append(contentMD5bytes[i].ToString("x2"));
        }

        string contentMD5 = sb.ToString();

        string stringToSign = string.Format("{0}\n{1}\n{2}\n{3}\n{4}", httpAction, contentMD5, contentType, date, requestPath);

        HMACSHA1 sha1 = new HMACSHA1(System.Text.Encoding.ASCII.GetBytes(secret_key));
        byte[] sha1Bytes = System.Text.Encoding.ASCII.GetBytes(stringToSign);
        MemoryStream stream = new MemoryStream(sha1Bytes);
        byte[] sha1Hash = sha1.ComputeHash(stream);
        string signature = System.Convert.ToBase64String(sha1Hash);

        unityWebRequest.SetRequestHeader("authorization", string.Format("VWS {0}:{1}", access_key, signature));


        unityWebRequest.SendWebRequest();
        while (!unityWebRequest.isDone && !unityWebRequest.isNetworkError) { }
        //If request error, return fail
        if (unityWebRequest.error != null)
        {
            Debug.Log("requestError: " + unityWebRequest.error);

            return "fail";
        }
        else
        {
            if(httpAction == "DELETE")
            {
                return "Deleted";
            }
            return unityWebRequest.downloadHandler.text;
        }
    }
}

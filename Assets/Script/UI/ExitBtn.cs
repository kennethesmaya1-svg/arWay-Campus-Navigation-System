using UnityEngine;

public class ExitBtn : MonoBehaviour
{
   public void OnClickExitButton()
   {
#if UNITY_EDITOR
       UnityEditor.EditorApplication.isPlaying = false;
#else
       Application.Quit();
#endif
   }
}

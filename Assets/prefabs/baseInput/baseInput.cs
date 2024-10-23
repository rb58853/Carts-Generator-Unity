using UnityEngine;
using TMPro;
using Cards;
using Config;

public class BaseInput : MonoBehaviour
{
    TMP_InputField inputField;
    TextMeshProUGUI textComponent;
    public string prop;

    void Start()
    {

    }

    public void SetProp(string prop)
    {
        //Asigna el nombre de la variable y los valores de la misma
        SetText(prop);
    }

    private void Awake()
    {
        inputField = GetComponentInChildren<TMP_InputField>();
        textComponent = GetComponentInChildren<TextMeshProUGUI>();

        if (inputField == null || textComponent == null)
        {
            Debug.LogError("CustomObject: Faltan componentes TMP_InputField o TextMeshProUGUI en los hijos.");
        }
        SetText(prop);
        SetValue(CardGeneration.Card.getField(prop)["value"]);
    }

    public void SetInputType(TMP_InputField.ContentType contentType)
    {
        inputField.contentType = contentType;
    }
    public void SetValue(dynamic value)
    {
        inputField.text = value.ToString();
    }

    public void SetText(string newText)
    {
        textComponent.text = newText;
    }
}
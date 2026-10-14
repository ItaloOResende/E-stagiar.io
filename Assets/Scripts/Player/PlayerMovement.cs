using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;   // sem teclado (ex.: testes em batchmode): só a gravidade

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed)
                input.y += 1;

            if (keyboard.sKey.isPressed)
                input.y -= 1;

            if (keyboard.aKey.isPressed)
                input.x -= 1;

            if (keyboard.dKey.isPressed)
                input.x += 1;
        }

        Vector3 direction = transform.right * input.x + transform.forward * input.y;

        controller.Move(direction.normalized * speed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
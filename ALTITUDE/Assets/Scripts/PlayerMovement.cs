using Unity.Mathematics;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    public float speed = 12, jumpHeight = 3, gravity = -9.81f;
    Vector3 velocity;
    bool onGround, gravityActive;
    public Light sun;

    void Update()
    {
        onGround = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        if (onGround && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        if (transform.position.y < -10)
        {
            sun.intensity = 1+(transform.position.y/20);
        }
        else
        {
            sun.intensity = 1;
        }
        sun.intensity = math.max(0, sun.intensity);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && onGround)
        {
            velocity.y = Mathf.Sqrt(jumpHeight*-2*gravity);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            velocity.y = -6;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            velocity.y = 6;
        }
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            velocity.y = 0;
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (gravityActive)
            {
                gravityActive = false;
            }
            else
            {
                gravityActive = true;
            }
        }
        if (gravityActive)
        {
            velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }
}

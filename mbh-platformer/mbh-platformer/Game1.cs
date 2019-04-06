using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PicoX;
using System.Collections.Generic;
using System;

namespace mbh_platformer
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : PicoXGame
    {
        public static Game1 inst;

        public class PicoXObj : PicoXGame
        {
            #region UNUSED_API
            public override string GetMapString()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<int, string> GetMusicPaths()
            {
                throw new NotImplementedException();
            }

            public override string GetPalTextureString()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<string, object> GetScriptFunctions()
            {
                throw new NotImplementedException();
            }

            public override List<string> GetSheetPath()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<int, string> GetSoundEffectPaths()
            {
                throw new NotImplementedException();
            }
            #endregion

            public PicoXObj()
            {
                if (inst == null)
                {
                    throw new InvalidOperationException("Attempting to create PicoXObj before inst is set.");
                }
                P8API = inst;
            }
        }

        public class sprite : PicoXObj
        {
            public float x;
            public float y;
            public float dx;
            public float dy;
            public int w;
            public int h;
            public int cw;
            public int ch;

            public bool is_solid = false;

            public struct anim
            {
                public int ticks;
                public int[][] frames;
                public bool? loop;
                public int? w;
                public int? h;
            }

            static public int[] create_anim_frame(int start_sprite, int w, int h, int zeroed_rows_at_top = 0)
            {
                int[] frame = new int[w * h];
                int count = 0;
                for (int j = 0; j < h; j++)
                {
                    for (int i = 0; i < w; i++)
                    {
                        int sprite_id = (start_sprite + i) + (16 * j);
                        if (j < zeroed_rows_at_top)
                        {
                            sprite_id = 0;
                        }
                        frame[count] = sprite_id;
                        count++;
                    }
                }

                return frame;
            }

            //animation definitions.
            //use with set_anim()
            public Dictionary<string, anim> anims;

            public string curanim = "";//currently playing animation
            public int curframe = 0;//curent frame of animation.
            public int animtick = 0;//ticks until next frame should show.
            public bool flipx = false;//show sprite be flipped.
            public bool flipy = false;

            public delegate void on_anim_done_delegate(string anim_name);
            public on_anim_done_delegate event_on_anim_done;

            //request new animation to play.
            public void set_anim(string anim)
            {
                var self = this;
                if (anim == self.curanim) return;//early out.
                var a = self.anims[anim];

                self.animtick = a.ticks;//ticks count down.
                self.curanim = anim;
                self.curframe = 0;
            }

            public void tick_anim()
            {
                animtick -= 1;
                if (animtick <= 0)
                {
                    curframe += 1;

                    var a = anims[curanim];
                    animtick = a.ticks; //reset timer
                    if (curframe >= a.frames.Length)
                    {
                        if (a.loop == false)
                        {
                            event_on_anim_done?.Invoke(curanim); // TODO_PORT
                            curframe--; // back up the frame counter
                        }
                        else
                        {
                            // TODO: Was it intentional that this is only called when looping?
                            event_on_anim_done?.Invoke(curanim); // TODO_PORT
                            curframe = 0; //loop
                        }
                    }
                }
            }

            public override void _update60()
            {
                base._update60();

                tick_anim();
            }

            public override void _draw()
            {
                var self = this;
                base._draw();

                var a = anims[curanim];
                int[] frame = a.frames[curframe];

                // TODO: Mono8 Port
                //if (pal) push_pal(pal)

                // Mono8 Port: Starting with table only style.
                //if type(frame) == "table" then
                {
                    var final_w = a.w ?? w;
                    var final_h = a.h ?? h;
                    var final_w_half = flr(final_w * 0.5f);
                    var final_h_half = flr(final_h * 0.5f);

                    var start_x = x - (final_w_half);
                    var start_y = y - (final_h_half);

                    var count = 0;

                    var num_vert = flr(final_h / 8);
                    var num_horz = flr(final_w / 8);

                    var inc_x = 8;
                    var inc_y = 8;

                    if (flipx)
                    {
                        start_x = start_x + ((num_horz - 1) * 8);
                        inc_x = -8;
                    }


                    if (flipy)
                    {
                        start_y = start_y + ((num_vert - 1) * 8);
                        inc_y = -8;
                    }

                    var y2 = start_y;

                    for (int v_count = 0; v_count < num_vert; v_count++)
                    {
                        var x2 = start_x;

                        for (int h_count = 0; h_count < num_horz; h_count++)
                        {
                            //draw in frame order, but from
                            // right to left.
                            var f = frame[count];

                            // Don't draw sprite 0. This allows us to use that as a special 
                            // sprite in our animation data.
                            if (f != 0)
                            {

                                var flipx2 = flipx;

                                var flipy2 = flipy;

                                // Mono8 Port: frame is an int can can't be null.
                                //if (f != null)
                                {
                                    // TODO: This doesn't properly support flipping collections of tiles (eg. turn a 3 tile high 
                                    // sprite upside down. it will flip each tile independently).
                                    if (f < 0)
                                    {
                                        f = (int)abs(f);

                                        flipx2 = !flipx2;
                                    }

                                    // Hack to allow flipping Y. Add 512 to your sprite id.
                                    if (f >= 9999)
                                    {
                                        f -= 9999;

                                        flipy2 = !flipy2;
                                    }

                                    sspr((f * 8) % 128, flr((f / 16)) * 8, 8, 8,
                                        x2, y2, 8, 8,
                                        flipx2, flipy2);

                                }
                            }
                            count += 1;

                            x2 += inc_x;

                        }
                        y2 += inc_y;

                    }
                }

                //pset(x, y, 14);
                //rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 14);
                //rect(x - cw / 2, y - ch / 2, x + cw / 2, y + ch / 2, 15);
            }
        }

        public class simple_fx : sprite
        {
            public simple_fx()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = false,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(112, 4, 3),
                                create_anim_frame(116, 4, 3),
                                create_anim_frame(120, 4, 3),
                            }
                        }
                    },
                };

                set_anim("explode");

                x = 64;
                y = 64;
                w = 32;
                h = 24;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs.Remove(this);
                };
            }
        }



        public class rock : sprite
        {
            public rock()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(832, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                x = 80;
                y = 964; ;// - 64;
                w = 16;
                h = 16;
                cw = 16;
                ch = 16;
            }

            public override void _update60()
            {
                base._update60();

                //y += 1.0f;

                if (inst.p.dash_time > 0 &&  inst.intersects_obj_obj(this, inst.p))
                {
                    Vector2 v = new Vector2(x - (x - inst.p.x) * 0.5f, y - (y - inst.p.y) * 0.5f);
                    inst.p.start_dash_bounce(ref v);
                }
            }
        }

        //make the player
        public class player : sprite
        {
            //todo: refactor with m_vec.

            public float max_dx = 1;//max x speed
            public float max_dy = 4;//max y speed
            public float jump_speed = -2.5f;//jump veloclity
            public float acc = 1.0f;//acceleration
            public float dcc = 0.0f;//decceleration
            public float air_dcc = 0.95f;//air decceleration
            public float grav = 0.18f;

            public float dash_time = 0;
            public int dash_count = 0;

            //helper for more complex
            //button press tracking.
            //todo: generalize button index.
            public class complex_button : PicoXObj
            {
                //state
                public bool is_pressed = false;//pressed this frame
                public bool is_down = false;//currently down
                public bool is_released = false;
                public int ticks_down = 0;//how long down

                public int btn_id;

                public complex_button(int button_id)
                {
                    btn_id = button_id;
                }

                public override void _update60()
                {
                    base._update60();

                    var self = this;

                    //start with assumption
                    //that not a new press.
                    self.is_pressed = false;
                    is_released = false;
                    if (btn(btn_id))
                    {
                        if (!self.is_down)
                        {

                            self.is_pressed = true;

                        }

                        self.is_down = true;

                        self.ticks_down += 1;
                    }
                    else
                    {
                        is_released = is_down;

                        self.is_down = false;

                        self.is_pressed = false;

                        self.ticks_down = 0;
                    }
                }
            }

            complex_button jump_button = new complex_button(4);
            complex_button dash_button = new complex_button(5);

            public int jump_hold_time = 0;//how long jump is held
            int min_jump_press = 0;//min time jump can be held
            int max_jump_press = 12;//max time jump can be held

            bool jump_btn_released = true;//can we jump again?
            public bool grounded = false;//on ground

            public int airtime = 0;//time since groundeds

            public player() : base()
            {
                //animation definitions.
                //use with set_anim()
                anims = new Dictionary<string, anim>()
                {
                    {
                        "stand",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(0, 4, 3),
                            }
                        }
                    },
                    {
                        "walk",
                        new anim()
                        {
                            loop = true,
                            ticks=10,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35, 48, 49, 50, 51 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(12, 4, 3),
                                create_anim_frame(48, 4, 3),
                                create_anim_frame(52, 4, 3),
                                create_anim_frame(48, 4, 3),
                            }
                        }
                    },
                    {
                        "jump",
                        new anim()
                        {
                            h = 32+8,
                            ticks=1,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(56-16, 4, 5, 1),
                            },//what frames are shown.
                        }
                    },
                    {
                        "slide",
                        new anim()
                        {
                            ticks=1,//how long is each frame shown.
                            frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35, 48, 49, 50, 51 } },//what frames are shown.
                        }
                    },
                    {
                        "dash",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            //frames= new int[][] { create_anim_frame(60-16, 4, 5, 1), },//what frames are shown.
                            frames= new int[][]
                            {
                                create_anim_frame(160, 4, 3),
                                create_anim_frame(164, 4, 3),
                                create_anim_frame(168, 4, 3),
                            },//what frames are shown.
                        }
                    },
                    {
                        "dash_air",
                        new anim()
                        {
                            h = 40,
                            ticks=5,//how long is each frame shown.
                            frames= new int[][] { create_anim_frame(60-16, 4, 5, 1), },
                        }
                    },
                };

                x = 80;
                y = 964;
                dx = 0;
                dy = 0;
                w = 32;
                h = 24;

                cw = 16;
                ch = 24;
            }

            public void start_dash_bounce(ref Vector2 hit_point)
            {
                dy = -8;
                dash_time = 0;
                dash_count = 0;
                inst.objs.Add(new simple_fx() { x = hit_point.X, y = y + h * 0.25f });
             }

            //call once per tick.
            public override void _update60()
            {
                var self = this;

                string next_anim = curanim;

                //todo: kill enemies.

                //track button presses
                var bl = btn(0); //left
                var br = btn(1); //right
                dash_button._update60();

                if (dash_button.is_pressed && dash_count == 0 && dash_time <= 0)
                {
                    dash_count = 1;
                    dash_time = 30;
                    dy = 0;
                    self.jump_hold_time = 0; // kill jump

                    if (br)
                    {
                        self.flipx = false;
                    }
                    else if (bl)
                    {
                        self.flipx = true;
                    }
                }

                bool is_dashing = dash_time > 0;

                //move left/right
                if (!is_dashing)
                {
                    if (bl == true)
                    {
                        self.dx -= self.acc;
                        br = false;//handle double press
                    }
                    else if (br == true)
                    {
                        self.dx += self.acc;
                    }
                    else
                    {
                        if (self.grounded)
                        {

                            self.dx *= self.dcc;
                        }
                        else
                        {
                            self.dx *= self.air_dcc;
                        }
                    }
                }

                dash_time = max(0, dash_time - 1);

                const float dash_speed = 2.0f;
                if (is_dashing)
                {
                    if (flipx)
                    {
                        dx = -dash_speed;
                    }
                    else
                    {
                        dx = dash_speed;
                    }
                }
                else if (dash_count == 0)
                {
                    //limit walk speed
                    self.dx = mid(-self.max_dx, self.dx, self.max_dx);
                }
                else
                {
                    self.dx = mid(-dash_speed, self.dx, dash_speed);
                }

                //move in x
                self.x += self.dx;

                //hit walls
                Vector2 hit_point = new Vector2();
                if (inst.collide_side(self, out hit_point))
                {
                    if (is_dashing)
                    {
                        start_dash_bounce(ref hit_point);
                        is_dashing = false;
                    }

                }

                //jump buttons
                self.jump_button._update60();

                //jump is complex.
                //we allow jump if:
                //	on ground
                //	recently on ground
                //	pressed btn right before landing
                //also, jump velocity is
                //not instant. it applies over
                //multiple frames.
                //if (!is_dashing)
                {
                    if (self.jump_button.is_down)
                    {
                        //is player on ground recently.
                        //allow for jump right after 
                        //walking off ledge.
                        var on_ground = (self.grounded || self.airtime < 5);
                        //was btn presses recently?
                        //allow for pressing right before
                        //hitting ground.
                        var new_jump_btn = self.jump_button.ticks_down < 10;
                        //is player continuing a jump
                        //or starting a new one?
                        if (self.jump_hold_time > 0 || (on_ground && new_jump_btn))
                        {
                            //if (self.jump_hold_time == 0) sfx(snd.jump);//new jump snd // TODO_PORT

                            self.jump_hold_time += 1;
                            //keep applying jump velocity
                            //until max jump time.
                            if (self.jump_hold_time < self.max_jump_press)
                            {

                                self.dy = self.jump_speed;//keep going up while held

                            }

                            dash_time = 0;
                            is_dashing = false;
                        }
                    }
                    else
                    {
                        if (jump_button.is_released && (self.jump_hold_time > 0 && self.jump_hold_time < self.max_jump_press))
                        {
                            self.dy = -1.0f;
                        }

                        self.jump_hold_time = 0;
                    }

                    //move in y
                    self.dy += self.grav;
                }

                self.dy = mid(-self.max_dy, self.dy, self.max_dy);
                if (!is_dashing) // re-eval is_dashing since we might have just started jumping.
                {
                    self.y += self.dy;
                }
                else
                {
                    self.dy = 0; // kill building pull down
                }

                //floor
                if (!inst.collide_floor(self))
                {
                    next_anim = ("jump");

                    self.grounded = false;
                    self.airtime += 1;
                }

                //roof
                inst.collide_roof(self);

                //handle playing correct animation when
                //on the ground.
                if (self.grounded && !is_dashing)
                {
                    if (br)
                    {
                        if (self.dx < 0)
                        {
                            //pressing right but still moving left.
                            next_anim = ("slide");
                        }
                        else
                        {
                            next_anim = ("walk");
                        }
                    }
                    else if (bl)
                    {
                        if (self.dx > 0)
                        {
                            //pressing left but still moving right.
                            next_anim = ("slide");
                        }
                        else
                        {
                            next_anim = ("walk");
                        }

                    }
                    else
                    {
                        next_anim = ("stand");
                    }

                }

                if (is_dashing)
                {
                    if (grounded)
                    {
                        next_anim = ("dash");
                    }
                    else
                    {
                        next_anim = "dash_air";
                    }
                }

                if (grounded && dash_time <= 0)
                {
                    dash_count = 0;
                }

                //flip
                if (!is_dashing)
                {
                    if (br)
                    {
                        self.flipx = false;
                    }
                    else if (bl)
                    {
                        self.flipx = true;
                    }
                }

                set_anim(next_anim);

                base._update60();
            }
        }

        //make the camera.
        public class cam : PicoXObj
        {

            player tar;//target to follow.
            Vector2 pos;

            //how far from center of screen target must
            //be before camera starts following.
            //allows for movement in center without camera
            //constantly moving.
            float pull_threshold = 16;

            //min and max positions of camera.
            //the edges of the level.
            Vector2 pos_min = new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
            Vector2 pos_max = new Vector2(368 - inst.Res.X * 0.5f, 1024 - inst.Res.Y * 0.5f);

            int shake_remaining = 0;
            float shake_force = 0;

            public cam(player target)
            {
                tar = target;
                pos = new Vector2(target.x, target.y);
            }
            public override void _update60()
            {
                var self = this;

                base._update60();

                self.shake_remaining = (int)max(0, self.shake_remaining - 1);

                //follow target outside of
                //pull range.
                if (pull_max_x() < self.tar.x)
                {

                    self.pos.X += min(self.tar.x - pull_max_x(), 4);

                }
                if (pull_min_x() > self.tar.x)
                {
                    self.pos.X += min((self.tar.x - pull_min_x()), 4);
                }


                if (pull_max_y() < self.tar.y)
                {
                    self.pos.Y += min(self.tar.y - pull_max_y(), 4);

                }
                if (pull_min_y() > self.tar.y)
                {
                    self.pos.Y += min((self.tar.y - pull_min_y()), 4);

                }

                //lock to edge
                if (self.pos.X < self.pos_min.X) self.pos.X = self.pos_min.X;
                if (self.pos.X > self.pos_max.X) self.pos.X = self.pos_max.X;
                if (self.pos.Y < self.pos_min.Y) self.pos.Y = self.pos_min.Y;
                if (self.pos.Y > self.pos_max.Y) self.pos.Y = self.pos_max.Y;

            }

            public Vector2 cam_pos()
            {

                var self = this;
                //calculate camera shake.
                var shk = new Vector2(0, 0);
                if (self.shake_remaining > 0)
                {
                    shk.X = rnd(self.shake_force) - (self.shake_force / 2);
                    shk.Y = rnd(self.shake_force) - (self.shake_force / 2);

                }
                return new Vector2(self.pos.X - (inst.Res.X * 0.5f) + shk.X, self.pos.Y - (inst.Res.Y * 0.5f) + shk.Y);

            }

            public float pull_max_x()
            {
                return pos.X + pull_threshold;
            }

            public float pull_min_x()
            {
                return pos.X - pull_threshold;
            }

            public float pull_max_y()
            {
                return pos.Y + pull_threshold;
            }

            public float pull_min_y()
            {
                return pos.Y - pull_threshold;

            }

            void shake(int ticks, float force)
            {
                shake_remaining = ticks;

                shake_force = force;
            }
        }

        player p;
        cam game_cam;

        List<PicoXObj> objs;

        //math
        ////////////////////////////////

        bool intersects_obj_obj(sprite a, sprite b)
        {
            //return intersects_box_box(a.x,a.y,a.w,a.h,b.x,b.y,b.w,b.h)
            return intersects_box_box(
                a.x, a.y, a.cw * 0.5f, a.ch * 0.5f,
                b.x, b.y, b.cw * 0.5f, b.ch * 0.5f);
        }

        bool intersects_obj_box(sprite a, float x1, float y1, float w1, float h1)
        {
            return intersects_box_box(a.x, a.y, a.cw * 0.5f, a.ch * 0.5f, x1, y1, w1, h1);
        }

        bool intersects_point_obj(float px, float py, sprite b)
        {
            return intersects_point_box(px, py, b.x, b.y, b.cw * 0.5f, b.ch * 0.5f);
        }

        //point to box intersection.
        bool intersects_point_box(float px, float py, float x, float y, float w, float h)
        {
            if (flr(px) >= flr(x - (w)) && flr(px) < flr(x + (w)) && flr(py) >= flr(y - (h)) && flr(py) < flr(y + (h)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //box to box intersection
        bool intersects_box_box(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
        {
            var xd = x1 - x2;

            var xs = w1 + w2;
            if (abs(xd) >= xs)
            {
                return false;
            }

            var yd = y1 - y2;

            var ys = h1 + h2;

            if (abs(yd) >= ys)
            {
                return false;
            }

            return true;
        }


        //check if pushing into side tile and resolve.
        //requires self.dx,self.x,self.y, and 
        //assumes tile flag 0 == solid
        //assumes sprite size of 8x8
        //check if pushing into side tile and resolve.
        //requires self.dx,self.x,self.y, and 
        //assumes tile flag 0 == solid
        bool collide_side(player self, out Vector2 hit_point)
        {
            //check for collision along inner-3rd
            //of sprite side.
            var offset_x = self.cw / 2.0f;
            var offset_y = self.ch / 3.0f;

            for (float i = -offset_y; i <= offset_y; i += 2) // for i=-(self.w/3),(self.w/3),2 do
            {

                if (fget(mget(flr((self.x + (offset_x)) / 8), flr((self.y + i) / 8)), 0))
                {
                    self.dx = 0;
                    self.x = (flr(((self.x + (offset_x)) / 8)) * 8) - (offset_x);
                    hit_point.X = self.x + (offset_x);
                    hit_point.Y = self.y + i;
                    return true;
                }

                if (fget(mget(flr((self.x - (offset_x)) / 8), flr((self.y + i) / 8)), 0))
                {
                    self.dx = 0;

                    self.x = (flr((self.x - (offset_x)) / 8) * 8) + 8 + (offset_x);
                    hit_point.X = self.x - (offset_x);
                    hit_point.Y = self.y + i;

                    return true;
                }

            }
            //didn't hit a solid tile.
            hit_point.X = 0;
            hit_point.Y = 0;

            return false;
        }


        //check if pushing into floor tile and resolve.
        //requires self.dx,self.x,self.y,self.grounded,self.airtime and 
        //assumes tile flag 0 or 1 == solid
        bool collide_floor(player self)
        {
            //only check for ground when falling.
            if (self.dy < 0)
            {
                return false;
            }

            var landed = false;
            //check for collision at multiple points along the bottom
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f;
            var offset_y = self.ch / 2.0f;
            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                var tile = mget(flr((self.x + i) / 8), flr((self.y + (offset_y)) / 8));
                if (fget(tile, 0) || (fget(tile, 1) && self.dy >= 0))
                {
                    self.dy = 0;
                    self.y = (flr((self.y + (offset_y)) / 8) * 8) - (offset_y);
                    self.grounded = true;

                    self.airtime = 0;

                    landed = true;

                }
            }
            return landed;
        }

        //check if pushing into roof tile and resolve.
        //requires self.dy,self.x,self.y, and 
        //assumes tile flag 0 == solid
        bool collide_roof(player self)
        {
            //check for collision at multiple points along the top
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f;
            var offset_y = self.ch / 2.0f;

            bool hit_roof = false;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                if (fget(mget(flr((self.x + i) / 8), flr((self.y - (offset_y)) / 8)), 0))
                {
                    self.dy = 0;
                    self.y = flr((self.y - (offset_y)) / 8) * 8 + 8 + (offset_y);
                    self.jump_hold_time = 0;
                    hit_roof = true;
                }
            }

            return hit_roof;
        }

        public Game1() : base()
        {
            // MUST BE DONE BEFORE ANY PICOXOBJ ARE CREATED
            inst = this;
        }

        public override void _init()
        {
            objs = new List<PicoXObj>();

            p = new player();
            objs.Add(p);
            objs.Add(new rock());
            game_cam = new cam(p);
        }

        public override void _update60()
        {
            for(int i = 0; i < objs.Count; i++)
            {
                objs[i]._update60();
            }
            game_cam._update60();
        }

        public override void _draw()
        {
            palt(0, false);
            palt(11, true);
            cls(0);

            camera(game_cam.cam_pos().X, game_cam.cam_pos().Y);
            map(0, 0, 0, 0, 16, 16);

            foreach (PicoXObj o in objs)
            {
                o._draw();
            }
        }

        public override string GetMapString()
        {
            return "Content/raw/test_map.tmx";
        }

        public override Dictionary<int, string> GetMusicPaths()
        {
            return new Dictionary<int, string>();
        }

        public override List<string> GetSheetPath()
        {
            return new List<string>() { "raw/platformer_sheet" };
        }

        public override Dictionary<int, string> GetSoundEffectPaths()
        {
            return new Dictionary<int, string>();
        }

        public override Dictionary<string, object> GetScriptFunctions()
        {
            return new Dictionary<string, object>();
        }

        public override string GetPalTextureString()
        {
            return "";
        }

        public Vector2 Res = new Vector2(160, 144);

        public override Tuple<int, int> GetResolution() { return new Tuple<int, int>((int)Res.X, (int)Res.Y); }
    }
}

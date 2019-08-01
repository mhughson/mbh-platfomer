using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PicoX;
using System.Collections.Generic;
using System;
using TiledSharp;

namespace mbh_platformer
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    /// 

    // WORKING ON:
    // Issue where you press into side of rock while it is going up, pulls player up.
    // See: "C:\Users\Matt\Documents\Mono8\Mono8_636902690048336832.gif"
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

            public virtual void _preupdate() { }
            public virtual void _postupdate() { }
        }

        //helper for more complex
        //button press tracking.
        public class complex_button : PicoXObj
        {
            //state
            public bool is_pressed = false;//pressed this frame
            public bool is_down = false;//currently down
            public bool is_released = false;//released this frame
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

        public class sprite : PicoXObj
        {
            public class flying_def
            {
                public bool horz = true;
                public int duration = 0;
                public int dist = 0;
            }
            public float x;
            public float y;
            public float x_initial;
            public float y_initial;
            public float dx;
            public float dy;
            public int w;
            public int h;
            public int cw;
            public int ch;
            public int cx_offset { get; protected set; } = 0;
            public int cy_offset { get; protected set; } = 0;
            public float cx { get { return x + cx_offset; } }
            public float cy { get { return y + cy_offset; } }
            public int jump_hold_time = 0;//how long jump is held
            public bool grounded = false;//on ground
            public int airtime = 0;//time since groundeds
            public float scaley = 0;

            public bool is_platform = false;
            public bool stay_on = false;

            public flying_def flying = null;

            public float hp_max = 1;
            public float hp = 1;
            public float attack_power = 1;

            protected int invul_time = 0;
            protected int invul_time_on_hit = 120;

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
                if (anims == null || !anims.ContainsKey(curanim))
                {
                    return;
                }

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
                            // back up the frame counter so that we sit on the last frame.
                            // do it before calling anim_done, because that might actually trigger
                            // a new animation and we don't want to mess with its frame.
                            curframe--;
                            event_on_anim_done?.Invoke(curanim); // TODO_PORT
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
                invul_time = (int)max(0, invul_time - 1);
                base._update60();

                tick_anim();
            }

            public override void _draw()
            {
                var self = this;
                base._draw();

                if (anims == null)
                {
                    return;
                }

                var a = anims[curanim];
                int[] frame = a.frames[curframe];

                // TODO: Mono8 Port
                //if (pal) push_pal(pal)

                // Mono8 Port: Starting with table only style.
                //if type(frame) == "table" then
                if (invul_time == 0 || invul_time % 2 == 0)
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
                                        x2, y2 - (scaley * v_count) + (((8) - (8 - scaley)) * num_vert), 8, 8 - scaley,
                                        flipx2, flipy2);

                                }
                            }
                            count += 1;

                            x2 += inc_x;

                        }
                        y2 += inc_y;

                    }
                }

                //if (inst.time_in_state % 2 == 0)
                {
                    rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 14);
                    rect(cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2, 15);
                }
                pset(x, y, 8);
                pset(cx, cy, 9);

                // bottom
                //var offset_x = self.cw / 3.0f;
                //var offset_y = self.ch / 2.0f;
                //for (float i = -(offset_x); i <= (offset_x); i += 2)
                //{
                //    pset(x + i, y + offset_y, 9);
                //}

                // sides

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

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                base._update60();
            }
        }

        public class simple_fx_death_spark : simple_fx
        {
            float dir_x;
            float dir_y;

            public simple_fx_death_spark(float dir_x, float dir_y) : base()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = true,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(224, 2, 2),
                                create_anim_frame(226, 2, 2),
                                create_anim_frame(228, 2, 2),
                                create_anim_frame(226, 2, 2),
                            }
                        }
                    },
                };

                set_anim("explode");

                x = 64;
                y = 64;
                w = 16;
                h = 16;

                this.dir_x = dir_x;
                this.dir_y = dir_y;

                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                x += dir_x;
                y += dir_y;
            }
        }

        public class simple_fx_rotor : simple_fx
        {
            public simple_fx_rotor()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "explode",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                // dupes to give it time to leave screen.
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                                create_anim_frame(324, 2, 1),
                                create_anim_frame(326, 2, 1),
                            }
                        }
                    },
                };

                set_anim("explode");

                w = 16;
                h = 8;
            }

            public override void _update60()
            {
                base._update60();

                dy += -0.1f; // grav
                y += dy;
            }
        }

        public class badguy : sprite
        {
            int local_ticks = 0;

            float max_dx = 9999;
            float max_dy = 9999;

            protected float grav = 0.1f;

            protected bool solid = true;

            bool bounced = false;

            public int dead_time = -1;

            public bool insta_death = false;

            bool cleared_attacker = false;

            public badguy()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(320, 2, 2),
                                create_anim_frame(322, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                dx = -0.5f;

                stay_on = true;
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                local_ticks += 1;

                //limit walk speed

                dx = mid(-max_dx, dx, max_dx);

                //move in x

                x += dx;


                if (solid)
                {
                    var old_dx = dx;
                    Vector2 hit_point;
                    if (inst.collide_side(this, out hit_point))
                    {
                        dx = -old_dx;
                    }
                }


                //move in y

                dy += grav;

                dy = mid(-max_dy, dy, max_dy);

                y += dy;

                // TODO:
                if (flying != null && dead_time == -1)
                {
                    var t60 = local_ticks / 60.0f;

                    if (flying.horz == true)
                    {
                        float start_x = x;
                        x = x_initial + ((sin(t60 / flying.duration) + 1) * 0.5f) * flying.dist;
                        if (x < start_x)
                        {
                            flipx = true;
                        }
                        else if (x > start_x)
                        {
                            flipx = false;
                        }
                    }
                    else
                    {
                        y = y_initial + ((sin(t60 / flying.duration) + 1) * 0.5f) * flying.dist;

                    }
                }


                if (solid)
                {
                    inst.collide_floor(this);
                    inst.collide_roof(this);
                }

                // ENEMY SPECIFIC:
                // 

                if (dx < 0)
                {
                    flipx = true;
                }
                else if (dx > 0) // let 0 maintain.
                {
                    flipx = false;
                }

                // TODO:
                if (dead_time >= 0)
                {
                    dead_time -= 1;

                    if (dead_time <= 0)
                    {
                        inst.objs.Remove(this);
                    }
                    else
                    {
                        if (!cleared_attacker)
                        {
                            if (!inst.intersects_obj_obj(inst.p, this))
                            {
                                cleared_attacker = true;
                            }
                        }

                        if (cleared_attacker && bounced && !insta_death && inst.intersects_obj_obj(inst.p, this))
                        {
                            if (inst.p.get_is_dashing() || inst.p.dashing_last_frame)
                            {
                                on_bounce(inst.p, true);
                                Vector2 pos = new Vector2(x, y);
                                inst.p.start_dash_bounce(ref pos);
                            }
                        }
                    }

                    return;
                }

                // TODO
                //if (dead_time == -1 && !inst.p.is_dead && inst.p.pipe == null)
                if (dead_time == -1 && inst.cur_game_state != game_state.gameplay_dead)
                {

                    if (inst.intersects_obj_obj(inst.p, this))
                    {
                        // TODO
                        //if (inst.p.star_time > 0)
                        //{
                        //    self: on_bounce(p1);
                        //}
                        //else

                        //if (insta_death)
                        //{
                        //    inst.p.on_take_hit(this);
                        //}
                        //else
                        {
                            //feet pos.
                            var player_bottom = inst.p.cy + (inst.p.ch * 0.5f);

                            if ((inst.p.get_is_dashing() || inst.p.dashing_last_frame) && !insta_death)
                            {
                                //Vector2 pos = new Vector2(x, y);
                                //inst.p.start_dash_bounce(ref pos);
                                //dx = inst.p.dx;
                                //inst.p.dx *= -1;
                                on_bounce(inst.p);

                                if (flying != null)
                                {
                                    Vector2 pos = new Vector2(x, y);
                                    inst.p.start_dash_bounce(ref pos);
                                }
                            }
                            else if (cy > player_bottom)
                            {
                                if (inst.p.dy >= 0 && !insta_death)
                                {
                                    on_stomp();
                                }
                            }
                            else
                            {
                                //self:on_attack(p1);
                                inst.p.on_take_hit(this);
                            }
                        }
                    }
                }
               
                base._update60();
            }

            protected virtual void on_bounce(sprite attacker, bool ignore_dead_time = false)
            {
                hp = max(0, hp - 1);

                if ((dead_time == -1 || ignore_dead_time) && hp == 0)
                {
                    dead_time = 240;

                    dx = Math.Sign(attacker.dx) * 0.5f;

                    dy = -3;

                    solid = false;

                    flipy = true;

                    bounced = true;

                    // TODO:
                    //flying = null;
                    grav = 0.1f;

                    cleared_attacker = false;

                }
            }

            protected virtual void on_stomp()
            {
                scaley = 4;
                dead_time = 60;
                dx = 0;
                dy = 0;
                float amount = -0.5f;
                if (btn(4))
                {
                    amount = -1.0f;
                }
                inst.p.dy = inst.p.max_dy * amount;
            }
        }

        public class chopper_body : badguy
        {
            public chopper_body()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(340, 2, 3),
                                create_anim_frame(342, 2, 3),
                            }
                        }
                    },
                };

                set_anim("default");
            }
        }

        public class chopper : badguy
        {
            public chopper()
            {
                flying = new flying_def()
                {
                    duration = 7,
                    dist = 96,
                    horz = true,
                };

                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(324, 2, 3),
                                create_anim_frame(326, 2, 3),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 24;
                cw = 16;
                ch = 16;
                cy_offset = 4;

                dx = 0;
                dy = 0;
                grav = 0;

                stay_on = false;
                flipx = true;
                solid = false;
            }

            protected override void on_stomp()
            {
                base.on_stomp();

                inst.objs.Add(new chopper_body()
                {
                    x = x,
                    y = y + 4,
                    flipx = flipx,
                    dx = 0,
                    dy = 0,
                });
                inst.objs.Add(new simple_fx_rotor()
                {
                    x = x,
                    y = y - 8,
                    flipx = flipx,
                    dx = 0,
                    dy = -0.5f,
                });
                inst.objs.Remove(this);
            }
        }

        public class lava_splash : badguy
        {
            public lava_splash()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(368, 3, 2),
                                create_anim_frame(371, 3, 2),
                                create_anim_frame(374, 3, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 24;
                h = 16;
                cw = 24;
                ch = 8;
                cy_offset = 4;

                dx = 0;

                insta_death = true;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs.Remove(this);
                };
            }

            public override void _update60()
            {
                base._update60();

                //if (curframe == 1)
                //{
                //    insta_death = true;
                //}
                //else
                //{
                //    insta_death = false;
                //}
            }
        }

        public class lava_blast_spawner : sprite
        {
            int ticks;
            float dir;
            int life_time;
            float speed;

            public lava_blast_spawner(float dir)
            {
                this.dir = dir;
                life_time = 10 * 5;
                speed = 16;
            }

            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                ticks++;

                if (ticks % 5 == 0)
                {
                    x += dir * speed;// * (rnd(8) + 8);
                    if (fget(mget(flr(x/8.0f), flr(y/8.0f)), 0) || !fget(mget(flr(x / 8.0f), flr(y / 8.0f) + 1), 0))
                    {
                        inst.objs.Remove(this);
                        return;
                    }
                    inst.objs.Add(new lava_splash() { x = x, y = y, flipx = dir < 0 ? true : false, /*, dx = rnd(1) * dir*/});
                }


                base._update60();
            }
        }

        public class lava_blaster : badguy
        {
            int idle_ticks = 0;

            public lava_blaster(float dir)
            {

                anims = new Dictionary<string, anim>()
                {
                    {
                        "idle",
                        new anim()
                        {
                            loop = true,
                            ticks=30,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(400, 3, 3),
                                create_anim_frame(403, 3, 3),
                            }
                        }
                    },
                    {
                        "open_mouth",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(406, 3, 3),
                                create_anim_frame(409, 3, 3),
                            }
                        }
                    },
                    {
                        "fire",
                        new anim()
                        {
                            loop = false,
                            ticks=5,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(409, 3, 3),
                                create_anim_frame(406, 3, 3),
                                create_anim_frame(400, 3, 3),
                            }
                        }
                    },
                };

                set_anim("idle");

                w = 24;
                h = 24;
                cw = 24;
                ch = 24;

                dx = 0;

                flipx = dir < 0;

                event_on_anim_done += delegate (string anim_name)
                {
                    if (dead_time > 0)
                    {
                        return;
                    }
                    if (anim_name == "open_mouth")
                    {
                        inst.objs.Add(new lava_blast_spawner(flipx ? -1 : 1) { x = x, y = y + 4 });
                        set_anim("fire");
                    }
                    else if (anim_name == "fire")
                    {
                        set_anim("idle");
                    }
                };
            }

            public override void _update60()
            {
                if (dead_time > 0)
                {
                    set_anim("open_mouth");
                }
                else if (curanim == "idle")
                {
                    idle_ticks++;
                    if (idle_ticks > 180)
                    {
                        set_anim("open_mouth");
                        idle_ticks = 0;
                    }
                }
                base._update60();
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

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                is_platform = true;
            }

            protected float tick = 0;
            protected bool hit_this_frame = false;
            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var self = this;

                hit_this_frame = false;

                // TODO: Error - This assumes a collision means you are standing on top!
                var touching_player = inst.intersects_box_box(inst.p.cx, inst.p.cy + inst.p.ch * 0.5f, inst.p.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p);

                var old_x = self.x;

                var old_y = self.y;

                //base._update60();
                tick += 0.0025f;
                //y = 964 - (cos(tick) * 64.0f);
                //y = 964;// - (cos(tick) * 64.0f);
                x += (cos(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);
                //y += (sin(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);

                if (touching_player)
                {
                    hit_this_frame = true;
                    inst.p.x += self.x - old_x;
                    inst.p.y += self.y - old_y;

                    //inst.p.dx = self.x - old_x;
                    //inst.p.dy = self.y - old_y;

                    //inst.p.platformed = true;
                }
                else
                {
                    inst.p.platformed = false;
                }

                //if (tick >= 0.5f)
                //{
                //    inst.change_meta_tile(flr(x / 8), flr(y / 8), new int[] { 868, 869, 884, 885 });
                //    inst.objs.Remove(this);
                //}

                // Should be handled by collide side.
                //if (inst.p.dash_time > 0 && inst.intersects_obj_obj(this, inst.p))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.x) * 0.5f, y - (y - inst.p.y) * 0.5f);
                //    inst.p.start_dash_bounce(ref v);
                //}
            }
        }

        public class rock_pendulum : rock
        {
            public override void _update60()
            {
                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                var self = this;

                hit_this_frame = false;

                // TODO: Error - This assumes a collision means you are standing on top!
                var touching_player = inst.intersects_box_box(inst.p.cx, inst.p.cy + inst.p.ch * 0.5f, inst.p.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p);

                var old_x = self.x;

                var old_y = self.y;

                //base._update60();
                tick += 0.0025f;
                //y = 964 - (cos(tick) * 64.0f);
                //y = 964;// - (cos(tick) * 64.0f);
                x += (cos(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);
                y += (sin(tick)) * 1.0f; //80 - (cos(tick) * 64.0f);

                if (touching_player)
                {
                    hit_this_frame = true;
                    inst.p.x += self.x - old_x;
                    inst.p.y += self.y - old_y;

                    //inst.p.dx = self.x - old_x;
                    //inst.p.dy = self.y - old_y;

                    //inst.p.platformed = true;
                }
                else
                {
                    inst.p.platformed = false;
                }

                if (tick >= 0.5f)
                {
                    inst.change_meta_tile(flr(x/8), flr(y/8), new int[] { 868, 869, 884, 885 });
                    inst.objs.Remove(this);
                }

                // Should be handled by collide side.
                //if (inst.p.dash_time > 0 && inst.intersects_obj_obj(this, inst.p))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.x) * 0.5f, y - (y - inst.p.y) * 0.5f);
                //    inst.p.start_dash_bounce(ref v);
                //}
            }
        }

        public class player_topdown : sprite
        {
            public float dest_x = 0;
            public float dest_y = 0;

            public float walk_speed = 1.0f;

            public Vector2 desired_dir = Vector2.Zero;

            public player_topdown()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "idle",
                        new anim()
                        {
                            loop = true,
                            ticks=4,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(224, 2, 2),
                                create_anim_frame(226, 2, 2),
                                create_anim_frame(228, 2, 2),
                                create_anim_frame(226, 2, 2),
                            }
                        }
                    },
                };

                set_anim("idle");
                
                w = 16;
                h = 16;

                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                // Calculate prior to moving so that if we reach the
                // destination, we still remember what direction we
                // we going.

                if (dest_x != x)
                {
                    float delta = dest_x - x;
                    float dir = Math.Sign(delta);
                    x += dir * min(walk_speed, abs(delta));
                }
                if (dest_y != y)
                {
                    float delta = dest_y - y;
                    float dir = Math.Sign(delta);
                    y += dir * min(walk_speed, abs(delta));
                }
                

                const float tile_size = 16;
                //Vector2 dest = new Vector2(dest_x, dest_y);

                //if (btnp(0) && desired_dir != -Vector2.UnitX && !fget(mget(flr(dest.X/8 - 2), flr(dest_y/8)), 0))
                //{
                //    desired_dir = -Vector2.UnitX;
                //}
                //else if (btnp(1) && desired_dir != Vector2.UnitX && !fget(mget(flr(dest.X/8 + 2), flr(dest_y/8)), 0))
                //{
                //    desired_dir = Vector2.UnitX;
                //}
                //else if (btnp(2) && desired_dir != -Vector2.UnitY && !fget(mget(flr(dest.X/8), flr(dest_y/8 - 2)), 0))
                //{
                //    desired_dir = -Vector2.UnitY;
                //}
                //else if (btnp(3) && desired_dir != Vector2.UnitY && !fget(mget(flr(dest.X/8), flr(dest_y/8 + 2)), 0))
                //{
                //    desired_dir = Vector2.UnitY;
                //}
                //else if (!btn(0) && !btn(1) && !btn(2) && !btn(3))
                //{
                //    desired_dir = Vector2.Zero;
                //}

                // Arrived at tile.
                if (dest_x == x && dest_y == y)
                {
                    //dest_x += desired_dir.X * tile_size;
                    //dest_y += desired_dir.Y * tile_size;

                    bool new_dest = false;

                    if (btn(0) && !new_dest)
                    {
                        dest_x -= tile_size;
                        // Are we trying to walk into a wall?
                        if (fget(mget(flr(dest_x / 8), flr(dest_y / 8)), 0))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(1) && !new_dest)
                    {
                        dest_x += tile_size;
                        if (fget(mget(flr(dest_x / 8), flr(dest_y / 8)), 0))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(2) && !new_dest)
                    {
                        dest_y -= tile_size;
                        if (fget(mget(flr(dest_x / 8), flr(dest_y / 8)), 0))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
                    if (btn(3) && !new_dest)
                    {
                        dest_y += tile_size;
                        if (fget(mget(flr(dest_x / 8), flr(dest_y / 8)), 0))
                        {
                            dest_x = x;
                            dest_y = y;
                        }
                        else
                        {
                            new_dest = true;
                        }
                    }
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
            public float acc = 1.0f;//0.15f;//acceleration
            public float dcc = 0.0f;//decceleration
            public float air_dcc = 0.95f;//air decceleration
            public float grav = 0.18f;

            public float dash_time = 0;
            public int dash_count = 0;

            complex_button jump_button = new complex_button(4);
            complex_button dash_button = new complex_button(5);

            int min_jump_press = 0;//min time jump can be held
            int max_jump_press = 12;//max time jump can be held

            bool jump_btn_released = true;//can we jump again?
            public bool platformed = false;

            int jump_count = 0;
            int max_jump_count = 2;

            // Hack to solve cash where player hits 2 flying enemies in the same frame.
            // First enemy starts playing dash bouncing, and second sees he isn't jumping
            // and does damage.
            // Note: I think a better, more general solution, would be to make player invul
            //       for a short time after dash bouncing, or possibly even let them attack
            //       during that time.
            public bool dashing_last_frame { get; private set; }

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
                    {
                        "taking_hit",
                        new anim()
                        {
                            h = 32,
                            ticks=1,//how long is each frame shown.
                            frames= new int[][]
                            {
                                create_anim_frame(172, 4, 4),
                            },//what frames are shown.
                        }
                    },
                };

                x = 159;
                y = 932 - 32 * 8;
                dx = 0;
                dy = 0;
                w = 32;
                h = 24;

                cw = 16;
                ch = 24;
                //cx_offset = 8;
                //cy_offset = 6;

                hp_max = 3;
                hp = 3;
            }

            public void start_dash_bounce(ref Vector2 hit_point)
            {
                dy = -8;
                dx = 5 * -Math.Sign(hit_point.X - cx);
                dash_time = 0;
                dash_count = 0;
                inst.objs.Add(new simple_fx() { x = hit_point.X, y = y + h * 0.25f });

                int mx = flr(hit_point.X / 8.0f);
                int my = flr(hit_point.Y / 8.0f);
                if (fget(mget(mx, my), 2))
                {
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 });
                    inst.objs.Add(new block_restorer(mx, my, 240));
                }
                if (fget(mget(mx, my), 3))
                {
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 });
                    Point map_point = inst.map_pos_to_meta_tile(mx, my);
                    inst.objs.Add(new rock_pendulum() { x = map_point.X * 8 + 8, y = map_point.Y * 8 + 8 });
                }

                inst.hit_pause.start_pause(hit_pause_manager.pause_reason.bounce);
            }

            public bool get_is_dashing()
            {
                return dash_time > 0;
            }

            public override void _preupdate()
            {
                base._preupdate();

                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead))
                {
                    return;
                }

                if (invul_time > (invul_time_on_hit * 0.6f))
                {
                    return;
                }
                
                if (dash_time > 0)
                {
                    dashing_last_frame = true;
                }
                else
                {
                    dashing_last_frame = false;
                }
            }

            //call once per tick.
            public override void _update60()
            {
                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead))
                {
                    return;
                }

                var self = this;

                string next_anim = curanim;

                if (invul_time > (invul_time_on_hit * 0.6f))
                {
                    x += dx;
                    set_anim("taking_hit");
                    base._update60();
                    return;
                }

                //if( inst.collide_floor(self))
                //{
                //    printh("hit floor");
                //}

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

                bool is_dashing = get_is_dashing();

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
                        if (!platformed)
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
                else if (!platformed)
                {
                    //limit walk speed
                    self.dx = mid(-self.max_dx, self.dx, self.max_dx);
                }
                //else
                //{
                //    self.dx = mid(-dash_speed, self.dx, dash_speed);
                //}

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
                        if (self.jump_hold_time > 0)
                        {
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
                        else if ((on_ground && new_jump_btn && jump_count == 0))
                        {
                            jump_hold_time += 1;
                            jump_count++;
                            dash_time = 0;
                            is_dashing = false;
                            self.dy = self.jump_speed;
                        }
                        else if (jump_count < max_jump_count && is_dashing && jump_button.is_pressed)
                        {
                            // Additional jumps get a little effect to show that it is special
                            inst.objs.Add(new simple_fx() { x = x, y = y + h * 0.5f });
                            jump_hold_time += 1;
                            jump_count++;
                            dash_time = 0;
                            is_dashing = false;
                            self.dy = self.jump_speed;
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
                    if (self.airtime >= 5 && jump_count == 0)
                    {
                        // We fell of a ledge. eat a jump so that you can't fall->jump->jump to get further.
                        jump_count++;
                    }
                }
                else
                {
                    jump_count = 0;
                }

                //roof
                inst.collide_roof(self);
                
                Tuple<float, float>[] hit_tests = new Tuple<float, float>[]
                {
                    new Tuple<float, float>(-cw * 0.25f, 0),
                    new Tuple<float, float>(cw * 0.25f, 0),
                    new Tuple<float, float>(0, -ch * 0.25f),
                    new Tuple<float, float>(0, ch * 0.25f),
                };

                // Search for spike in towards the center of 4 edges of player.
                foreach (var h in hit_tests)
                {
                    int cell_x = flr(((cx + h.Item1) / 8));
                    int cell_y = flr(((cy + h.Item2) / 8));
                    if (fget(mget(cell_x, cell_y), 1))
                    {
                        // placeholder to do massive damage.
                        sprite temp = new sprite()
                        {
                            attack_power = float.MaxValue,
                        };

                        on_take_hit(temp);

                        break;
                    }
                }

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

            public void on_take_hit(sprite attacker)
            {
                if (invul_time > 0)
                {
                    return;
                }

                hp -= attacker.attack_power;
                invul_time = 120;
                dx = Math.Sign(cx - attacker.cx) * 0.25f;
                dy = 0;
                jump_hold_time = 0;

                if (hp <= 0)
                {
                    inst.set_game_state(game_state.gameplay_dead);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.death);

                    Tuple<float, float>[] death_dirs = new Tuple<float, float>[]
                    {
                        new Tuple<float, float>(-1.0f, 0.0f),
                        new Tuple<float, float>( 1.0f, 0.0f),
                        new Tuple<float, float>( 0.0f, 1.0f),
                        new Tuple<float, float>( 0.0f,-1.0f),
                        new Tuple<float, float>(-0.7f,-0.7f),
                        new Tuple<float, float>( 0.7f,-0.7f),
                        new Tuple<float, float>( 0.7f, 0.7f),
                        new Tuple<float, float>(-0.7f, 0.7f),
                    };

                    foreach (var dir in death_dirs)
                    {
                        simple_fx_death_spark o = new simple_fx_death_spark(dir.Item1, dir.Item2)
                        {
                            x = x,
                            y = y,
                        };
                        inst.objs.Add(o);
                    }
                }
            }

            public override void _draw()
            {
                if (inst.cur_game_state != game_state.gameplay_dead)
                {
                    base._draw();
                }
            }
        }

        //make the camera.
        public class cam : PicoXObj
        {

            sprite tar;//target to follow.
            Vector2 pos;

            //how far from center of screen target must
            //be before camera starts following.
            //allows for movement in center without camera
            //constantly moving.
            float pull_threshold = 16;

            //min and max positions of camera.
            //the edges of the level.
            public Vector2 pos_min = new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
            public Vector2 pos_max = new Vector2(368 - inst.Res.X * 0.5f, 1024 - inst.Res.Y * 0.5f);

            int shake_remaining = 0;
            float shake_force = 0;

            public cam(sprite target)
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

            //public void activate_objs()
            //{
            //    for (int i = Game.game_world.cur_area.objs_queue.Count - 1; i >= 0; i--)
            //    {
            //        obj v = Game.game_world.cur_area.objs_queue[i];

            //        Rectangle area = spawn_rect();
            //        if (v.x <= (area.Right + v.w_half()) && v.x >= (area.Left - v.w_half()) && v.y <= (area.Bottom + v.h_half()) && v.y >= (area.Top - v.h_half()))
            //        //if (v.x < x)
            //        {
            //            // move to active list.
            //            v.activate();
            //        }
            //    }
            //}

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

        //math
        ////////////////////////////////

        bool intersects_obj_obj(sprite a, sprite b)
        {
            //return intersects_box_box(a.x,a.y,a.w,a.h,b.x,b.y,b.w,b.h)
            return intersects_box_box(
                a.cx, a.cy, a.cw * 0.5f, a.ch * 0.5f,
                b.cx, b.cy, b.cw * 0.5f, b.ch * 0.5f);
        }

        bool intersects_obj_box(sprite a, float x1, float y1, float w1, float h1)
        {
            return intersects_box_box(a.cx, a.cy, a.cw * 0.5f, a.ch * 0.5f, x1, y1, w1, h1);
        }

        bool intersects_point_obj(float px, float py, sprite b)
        {
            return intersects_point_box(px, py, b.cx, b.cy, b.cw * 0.5f, b.ch * 0.5f);
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
        bool collide_side(sprite self, out Vector2 hit_point)
        {
            // Don't do collision of the player isn't moving sideways.
            //if (self.dx == 0)
            //{
            //    hit_point = Vector2.Zero;
            //    return false;
            //}


            //didn't hit anything solid.
            hit_point.X = 0;
            hit_point.Y = 0;

            //check for collision along inner-2-3rds
            //of sprite side.
            var offset_x = self.cw / 2.0f;
            var offset_y = self.ch / 3.0f;
            float correction_x = 0.0f;
            float dir = 1.0f;
            bool hit = false;
            //if (self.dx < 0)
            //{
            //    dir = -1.0f;
            //    offset_x *= -1.0f;
            //    correction_x = 8.0f;
            //}

            // Right Tiles
            for (float i = -offset_y; i <= offset_y; i += 2) // for i=-(self.w/3),(self.w/3),2 do
            {

                if (fget(mget(flr((self.cx + (offset_x)) / 8), flr((self.cy + i) / 8)), 0))
                {
                    self.dx = 0;
                    self.x = (flr(((self.cx + (offset_x)) / 8)) * 8) + correction_x - (offset_x) - (self.cx_offset);
                    hit_point.X = self.cx + (offset_x);
                    hit_point.Y = self.cy + i;
                    //return true;
                    hit = true;
                }

            }

            // Left Tiles
            dir = -1.0f;
            offset_x *= -1.0f;
            correction_x = 8.0f;
            for (float i = -offset_y; i <= offset_y; i += 2) // for i=-(self.w/3),(self.w/3),2 do
            {
                if (fget(mget(flr((self.cx + (offset_x)) / 8), flr((self.cy + i) / 8)), 0))
                {
                    self.dx = 0;
                    self.x = (flr(((self.cx + (offset_x)) / 8)) * 8) + correction_x - (offset_x) - (self.cx_offset);
                    hit_point.X = self.cx + (offset_x) - 1;
                    hit_point.Y = self.cy + i;
                    //return true;
                    hit = true;
                }

            }

            // Not hitting a tile, so try dynamic objects.
            foreach (PicoXObj o in objs)
            {
                sprite v = o as sprite;
                if (v != null)
                {
                    // Only player for now.
                    if (self == p && v.is_platform)
                    {
                        // Left objects.

                        // check for collision minus the top 2 pixels and the bottom 2 pixels (hence -4)
                        //if (intersects_obj_box(self, v.x, v.y, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        if (intersects_box_box(self.cx - self.cw * 0.5f, self.cy, 0.5f, self.ch / 3.0f, v.cx, v.cy, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        {
                            self.dx = 0;
                            //self.x = (/*flr*/(v.x - (v.cw * dir) * 0.5f)) - ((self.cw * dir) * 0.5f);
                            // +1 is to fix a bug where the player seems to get sucked into the side of platforms
                            // when pushed.
                            self.x = (/*flr*/(v.cx + v.cw * 0.5f)) + (self.cw * 0.5f) - self.cx_offset + 1.0f;

                            // We don't really know the hit point, so just put it at the center on the edge that hit.
                            hit_point.X = self.cx + (offset_x);
                            hit_point.Y = self.cy;

                            //return true;
                            hit = true;
                        }

                        // Right objects.

                        if (intersects_box_box(self.cx + self.cw * 0.5f, self.cy, 0.5f, self.ch / 3.0f, v.cx, v.cy, v.cw * 0.5f, (v.ch - 4) * 0.5f))
                        {
                            self.dx = 0;
                            self.x = (/*flr*/(v.cx - v.cw * 0.5f)) - (self.cw * 0.5f) - self.cx_offset - 1.0f;

                            // We don't really know the hit point, so just put it at the center on the edge that hit.
                            hit_point.X = self.cx + (offset_x);
                            hit_point.Y = self.cy;

                            //return true;
                            hit = true;
                        }
                    }
                }
            }

            //return false;
            return hit;
        }


        //check if pushing into floor tile and resolve.
        //requires self.dx,self.x,self.y,self.grounded,self.airtime and 
        //assumes tile flag 0 or 1 == solid
        bool collide_floor(sprite self)
        {
            //only check for ground when falling.
            if (self.dy < 0)
            {
                return false;
            }

            //check for collision at multiple points along the bottom
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f; // only check inner 2-3rds
            var offset_y = self.ch / 2.0f;

            float? new_y = null;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                var box_x = self.cx;
                var box_y = self.cy;
                var box_w_half = self.cw * 0.5f;
                var box_h_half = self.ch * 0.5f;


                var y = flr((box_y + box_h_half) / 8);
                var y_actual = flr((self.y + box_h_half) / 8);

                if (fget(mget(flr((box_x + i) / 8), y), 0))
                {
                    new_y = (flr(y) * 8) - box_h_half + (self.y - self.cy);
                    break;
                }
            }

            // If we didn't hit a tile, try dynamic objects.
            if (!new_y.HasValue)
            {
                foreach (PicoXObj o in objs)
                {
                    sprite v = o as sprite;
                    if (v != null)
                    {
                        if (self == p && v.is_platform)
                        {
                            // Check a 1 pixel high box along the bottom the the player.
                            // Adding 2 to the solid because that is what solids do in their update to stick to
                            // objects when moving away from them.
                            if (inst.intersects_box_box(self.cx, self.cy + self.ch * 0.5f, self.cw * 0.5f, 1, v.cx, v.cy, v.cw * 0.5f, (v.ch + 2) * 0.5f))
                            {
                                new_y = (/*flr*/(v.cy - v.ch * 0.5f)) - (self.ch * 0.5f) - (self.cy_offset);
                                break;
                            }
                        }
                    }
                }
            }
            
            if (new_y.HasValue)
            {
                self.dy = 0;
                self.y = new_y.Value;
                self.grounded = true;
                self.airtime = 0;
                return true;
            }

            if (self.stay_on)
            {
                self.dx *= -1;
                self.x += self.dx;
            }

            return false;
        }

        //check if pushing into roof tile and resolve.
        //requires self.dy,self.x,self.y, and 
        //assumes tile flag 0 == solid
        bool collide_roof(sprite self)
        {
            if (self.dy > 0)
            {
                return false;
            }

            //check for collision at multiple points along the top
            //of the sprite: left, center, and right.
            var offset_x = self.cw / 3.0f; // check the inner 2 3rds
            var offset_y = self.ch / 2.0f;

            bool hit_roof = false;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                if (fget(mget(flr((self.cx + i) / 8), flr((self.cy - (offset_y)) / 8)), 0))
                {
                    self.dy = 0;
                    self.y = flr((self.cy - (offset_y)) / 8) * 8 + 8 + (offset_y) - self.cy_offset;
                    self.jump_hold_time = 0;
                    hit_roof = true;
                    break;
                }
            }

            if (!hit_roof)
            {
                foreach (PicoXObj o in objs)
                {
                    sprite v = o as sprite;
                    if (v != null)
                    {
                        if (self == p && v.is_platform)
                        {
                            // Check a 1 pixel box along the bottom of the player.
                            // Using 0.5f because that seems more correct but im not totally sure.
                            if (inst.intersects_box_box(self.cx, self.cy - self.ch * 0.5f, self.cw * 0.5f, 0.5f, v.cx, v.cy, v.cw * 0.5f, (v.ch) * 0.5f))
                            {
                                // Take the dy of the player or the solid, which ever is more downward.
                                // This ensure that the player doesn't kind of "float" along the bottom of the
                                // solid. We also min it to 0 so that if both are moving upwards, the player is
                                // at least stopped.
                                self.dy = min(0, max(v.dy, self.dy));
                                self.y = (/*flr*/(v.cy + v.ch * 0.5f)) + (self.ch * 0.5f) - self.cy_offset;
                                self.jump_hold_time = 0;
                                hit_roof = true;

                                break;
                            }
                        }
                    }
                }
            }

            return hit_roof;
        }

        public class hit_pause_manager : PicoXObj
        {
            public enum pause_reason
            {
                bounce,
                death,
            }

            Dictionary<pause_reason, int> pause_times = new Dictionary<pause_reason, int>()
            {
                { pause_reason.bounce, 0 }, // no pause for now. happens too much.
                { pause_reason.death, 30 }
            };

            public int pause_time_remaining { get; protected set; }

            public hit_pause_manager()
            {
                pause_time_remaining = 0;
            }

            public void start_pause(pause_reason reason)
            {
                pause_time_remaining = (int)inst.max(pause_time_remaining, pause_times[reason]);
            }

            public override void _update60()
            {
                base._update60();

                pause_time_remaining = (int)inst.max(0, pause_time_remaining - 1);
            }

            public bool is_paused()
            {
                return pause_time_remaining > 0;
            }
        }

        public class block_restorer : PicoXObj
        {
            public int map_x;
            public int map_y;
            public int time_remaining;

            public block_restorer(int map_x, int map_y, int life_span) : base()
            {
                this.map_x = map_x;
                this.map_y = map_y;
                time_remaining = life_span;
            }

            public override void _update60()
            {
                base._update60();

                time_remaining -= 1;

                if (time_remaining <= 0)
                {
                    inst.change_meta_tile(map_x, map_y, new int[] { 834, 835, 850, 851});
                    inst.objs.Remove(this);
                }
            }
        }

        public enum game_state
        {
            main_menu,
            gameplay,
            gameplay_dead,
            game_over,

        }

        player p;
        cam game_cam;

        List<PicoXObj> objs;

        game_state cur_game_state;
        uint time_in_state;
        complex_button start_game;

        public hit_pause_manager hit_pause;

        public Game1() : base()
        {
            // MUST BE DONE BEFORE ANY PICOXOBJ ARE CREATED
            inst = this;
        }

        public void set_game_state(game_state new_state)
        {
            // Leaving...
            switch(cur_game_state)
            {
                case game_state.main_menu:
                    {
                        // main_menu -> gameplay
                        if (new_state == game_state.gameplay)
                        {

                        }
                        break;
                    }

                case game_state.gameplay_dead:
                    {
                        if (new_state == game_state.game_over)
                        {
                            objs.Clear();
                        }
                        break;
                    }
            }

            cur_game_state = new_state;
            time_in_state = 0;

            // Entering...
            switch (cur_game_state)
            {
                case game_state.gameplay:
                    {
                        reloadmap(GetMapString());

                        Vector2 spawn_point = Vector2.Zero;
                        Vector2 cam_area_min = Vector2.Zero;
                        Vector2 cam_area_max = Vector2.Zero;

                        TmxMap TmxMapData = new TmxMap(GetMapString());

                        foreach (var group in TmxMapData.ObjectGroups)
                        {
                            foreach (var o in group.Objects)
                            {
                                if (string.Compare(o.Type, "spawn_point", true) == 0)
                                {
                                    spawn_point = new Vector2((float)o.X + ((float)o.Width * 0.5f), (float)o.Y + ((float)o.Height * 0.5f));
                                }
                                else if (string.Compare(o.Type, "cam_area", true) == 0)
                                {
                                    cam_area_min = new Vector2((float)o.X, (float)o.Y);
                                    cam_area_max = new Vector2((float)o.X + (float)o.Width, (float)o.Y + (float)o.Height);
                                }
                                else if (string.Compare(o.Type, "spawn_chopper", true) == 0)
                                {
                                    objs.Add(
                                            new chopper()
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                            }
                        }

                        p = new player()
                        {
                            x = spawn_point.X,
                            y = spawn_point.Y,
                        };
                        objs.Add(new rock() { x = 37 * 8, y = 97 * 8, });
                        objs.Add(new rock() { x = 37 * 8, y = 89 * 8, });
                        objs.Add(new rock() { x = 27 * 8, y = 107 * 8, });
                        for (int i = 0; i < 10; i++)
                        {
                            objs.Add(new badguy() { x = 27 * 8 + i * 16, y = 107 * 8 });
                        }
                        objs.Add(new badguy() { x = 19 * 8, y = 97 * 8 });
                        objs.Add(new chopper() { x = 31 * 8, y = 85 * 8 });
                        objs.Add(new chopper() { x = 35 * 8, y = 80 * 8 });
                        objs.Add(new chopper() { x = 39 * 8, y = 75 * 8 });
                        objs.Add(new chopper() { x = 43 * 8, y = 70 * 8 });
                        objs.Add(new lava_splash() { x = 19 * 8, y = 97 * 8 });
                        objs.Add(new lava_blaster(1) { x = 9 * 8, y = 93 * 8 });
                        objs.Add(new lava_blaster(-1) { x = 40 * 8, y = 48 * 8 });
                        objs.Add(p);
                        player_topdown pt = new player_topdown()
                        {
                            x = flr(spawn_point.X / 16.0f) * 16.0f + 8.0f,
                            y = flr(spawn_point.Y / 16.0f) * 16.0f + 8.0f,
                        };
                        pt.dest_x = pt.x;
                        pt.dest_y = pt.y;

                        objs.Add(pt);
                        game_cam = new cam(pt)
                        {
                            pos_min = cam_area_min + new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f),
                            pos_max = cam_area_max - new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f),
                        };

                        foreach(PicoXObj o in objs)
                        {
                            sprite s = o as sprite;
                            if (s != null)
                            {
                                s.x_initial = s.x;
                                s.y_initial = s.y;
                            }
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        public Point map_pos_to_meta_tile(int x, int y)
        {
            x = (x % 2 == 0) ? x : x - 1;
            y = (y % 2 == 0) ? y : y - 1;
            return new Point(x, y);
        }

        public void change_meta_tile(int x, int y, int[] t)
        {
            Point final_pos = map_pos_to_meta_tile(x, y);
            x = final_pos.X;
            y = final_pos.Y;
            
            int count = 0;

            for (int j = 0; j <= 1; j++)
            {
                for (int i = 0; i <= 1; i++)
                {
                    mset(x + i, y + j, t[count]);
                    count += 1;
                }
            }
        }

        public void change_meta_tile(int x, int y, int tile_id)
        {
            change_meta_tile(x, y, new int[] { tile_id, tile_id, tile_id, tile_id });
        }

        public override void _init()
        {
            objs = new List<PicoXObj>();
            start_game = new complex_button(4);
            hit_pause = new hit_pause_manager();
            cur_game_state = game_state.main_menu;
            reloadmap("");
        }

        public override void _update60()
        {
            time_in_state++;

            start_game._update60();

            switch (cur_game_state)
            {
                case game_state.main_menu:
                    {
                        if (start_game.is_released)
                        {
                            set_game_state(game_state.gameplay);
                        }
                        break;
                    }
                case game_state.gameplay:
                    {
                        //if (time_in_state % 120 == 0)
                        //{
                        //    objs.Add(new lava_blast_spawner() { x = 29 * 8, y = 93 * 8 });
                        //}
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        if (time_in_state >= 240)
                        {
                            set_game_state(game_state.game_over);
                        }
                        break;
                    }
                case game_state.game_over:
                    {
                        if (start_game.is_released)
                        {
                            set_game_state(game_state.gameplay);
                        }
                        break;
                    }
            }
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._preupdate();
            }
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._update60();
            }
            for (int i = 0; i < objs.Count; i++)
            {
                objs[i]._postupdate();
            }
            if (game_cam != null)
            {
                game_cam._update60();
            }
            if (hit_pause != null)
            {
                hit_pause._update60();
            }
        }

        public override void _draw()
        {
            pal();
            palt(0, false);
            palt(11, true);
            cls(0);

            if (game_cam != null)
            {
                camera(game_cam.cam_pos().X, game_cam.cam_pos().Y);
            }
            else
            {
                camera(0, 0);
            }

            switch (cur_game_state)
            {
                case game_state.gameplay:
                case game_state.gameplay_dead:
                    {
                        //pal(7, 0);
                        //pal(6, 5);
                        //pal(5, 6);
                        //pal(0, 7);
                        map(0, 0, 0, 0, 16, 16);
                        //map(0, 0, 0, 0, 16, 16, 0, 1); // easy mode?
                        //pal();
                        break;
                    }
            }

            foreach (PicoXObj o in objs)
            {
                o._draw();
            }

            // HUD

            Action draw_health = () =>
            {
                if (p == null)
                {
                    return;
                }

                int y_pos = 1;

                for (int i = 0; i < p.hp_max; i++)
                {
                    int id = 238;
                    if (i < p.hp)
                    {
                        id = 230;
                    }
                    spr(id, 1, y_pos, 2, 2);
                    y_pos += 16;
                }
            };

            camera(0, 0);

            draw_health();

            switch (cur_game_state)
            {
                case game_state.main_menu:
                    {
                        var str = "dash maximus";
                        print(str, 128 - (str.Length * 0.5f) * 4, 120, 7);
                        str = "-dx-";
                        print(str, 128 - (str.Length * 0.5f) * 4, 120 + 6, 7);
                        break;
                    }
                case game_state.gameplay:
                    {
                        if (time_in_state < 15)
                        {
                            pal(7, 5, 1);
                            pal(6, 0, 1);
                            pal(5, 0, 1);
                            pal(0, 0, 1);
                        }
                        else if (time_in_state < 30)
                        {
                            pal(7, 6, 1);
                            pal(6, 5, 1);
                            pal(5, 0, 1);
                            pal(0, 0, 1);
                        }
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        if (time_in_state < 120)
                        {

                        }
                        else if (time_in_state < 150)
                        {
                            pal(7, 6, 1);
                            pal(6, 5, 1);
                            pal(5, 0, 1);
                            pal(0, 0, 1);
                        }
                        else if (time_in_state < 180)
                        {
                            pal(7, 5, 1);
                            pal(6, 0, 1);
                            pal(5, 0, 1);
                            pal(0, 0, 1);
                        }
                        else
                        {
                            pal(7, 0, 1);
                            pal(6, 0, 1);
                            pal(5, 0, 1);
                            pal(0, 0, 1);
                        }
                        break;
                    }
                case game_state.game_over:
                    {
                        var str = "game over";
                        print(str, 128 - (str.Length * 0.5f) * 4, 120, 7);
                        break;
                    }
            }


            string btnstr = "";
            for (int i = 0; i < 6; i++)
            {
                btnstr += btn(i) ? "1" : "0";
                btnstr += " ";
            }

            print(btnstr, 0, Res.Y - 4, 0);
        }

        public override string GetMapString()
        {
            return "Content/raw/test_map_2.tmx";
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

        public Vector2 Res = new Vector2(256, 240); // NES
        //public Vector2 Res = new Vector2(160, 144); // GB

        public override Tuple<int, int> GetResolution() { return new Tuple<int, int>((int)Res.X, (int)Res.Y); }

        public override int GetGifScale() { return 2; }
    }
}

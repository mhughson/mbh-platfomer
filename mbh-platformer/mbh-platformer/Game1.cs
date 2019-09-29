using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PicoX;
using System.Collections.Generic;
using System;
using TiledSharp;
using System.IO;
using System.Linq;

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
            public byte grounded = 0;//on ground
            public int airtime = 0;//time since groundeds
            public float scaley = 0;

            public bool is_platform = false;
            public bool stay_on = false;
            public bool launched = false;

            public flying_def flying = null;
            public virtual int get_hp_max() { return 1; }
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

                if (anims != null && !String.IsNullOrEmpty(curanim))
                {

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
                }

                //if (inst.time_in_state % 2 == 0)
                //{
                //    rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, 14);
                //    rect(cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2, 15);
                //}
                //pset(x, y, 8);
                //pset(cx, cy, 9);

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
                    inst.objs_remove_queue.Add(this);
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

        public class simple_fx_particle : simple_fx
        {
            public simple_fx_particle(float dir_x, float dir_y, int sprite_id)
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            loop = true,
                            ticks=1,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(sprite_id, 1, 1),
                            }
                        }
                    },
                };

                set_anim("default");

                x = 0;
                y = 0;
                w = 8;
                h = 8;

                dx = dir_x;
                dy = dir_y;


                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused())
                {
                    return;
                }

                x += dx;
                dy += 0.3f; // grav
                y += dy;

                if (inst.game_cam.is_obj_off_screen(this))
                {
                    inst.objs_remove_queue.Add(this);
                }
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

            public bool touch_damage = false;

            bool cleared_attacker = false;

            protected bool has_rock_armor = false;

            public badguy(float dir)
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

                dx = 0.5f * dir;

                flipx = dir < 0;

                stay_on = true;

                hp = 1;
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

                if (!launched)
                {
                    x += dx;
                }


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

                grounded = 0;
                if (solid)
                {
                    Vector2 hit_point;
                    if (inst.collide_floor(this, out hit_point))
                    {
                        launched = false;
                    }
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
                        inst.objs_remove_queue.Add(this);
                    }
                    else
                    {
                        if (!cleared_attacker)
                        {
                            if (!inst.intersects_obj_obj(inst.pc.pawn, this))
                            {
                                cleared_attacker = true;
                            }
                        }

                        if (cleared_attacker && (bounced || launched) && !touch_damage && inst.intersects_obj_obj(inst.pc.pawn, this))
                        {
                            if (inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame)
                            {
                                on_bounce(inst.pc.pawn, true);
                                Vector2 pos = new Vector2(x, y);
                                inst.pc.pawn.start_dash_bounce(ref pos);
                            }
                        }
                    }

                    return;
                }

                if (launched)
                {
                    if (!cleared_attacker)
                    {
                        if (!inst.intersects_obj_obj(inst.pc.pawn, this))
                        {
                            cleared_attacker = true;
                        }
                    }

                    if (cleared_attacker && launched && !touch_damage && inst.intersects_obj_obj(inst.pc.pawn, this))
                    {
                        if (inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame)
                        {
                            on_bounce(inst.pc.pawn, true);
                            Vector2 pos = new Vector2(x, y);
                            inst.pc.pawn.start_dash_bounce(ref pos);
                            launched = false;
                        }
                    }
                }

                // TODO
                //if (dead_time == -1 && !inst.p.pawn.is_dead && inst.p.pawn.pipe == null)
                if (dead_time == -1 && inst.cur_game_state != game_state.gameplay_dead)
                {

                    if (inst.intersects_obj_obj(inst.pc.pawn, this))
                    {
                        // TODO
                        //if (inst.p.pawn.star_time > 0)
                        //{
                        //    self: on_bounce(p1);
                        //}
                        //else

                        //if (touch_damage)
                        //{
                        //    inst.p.pawn.on_take_hit(this);
                        //}
                        //else
                        {
                            //feet pos.
                            var player_bottom = inst.pc.pawn.cy + (inst.pc.pawn.ch * 0.5f);

                            if ((inst.pc.pawn.get_is_dashing() || inst.pc.pawn.dashing_last_frame) && !touch_damage)
                            {
                                bool rock_smashable = (inst.pc.found_artifacts & artifacts.rock_smasher) != 0 && has_rock_armor;
                                bool had_rock_armor = has_rock_armor;

                                //Vector2 pos = new Vector2(x, y);
                                //inst.p.pawn.start_dash_bounce(ref pos);
                                //dx = inst.p.pawn.dx;
                                //inst.p.pawn.dx *= -1;
                                if (!has_rock_armor)
                                {
                                    on_bounce(inst.pc.pawn);
                                }
                                else if (rock_smashable)
                                {
                                    int mx = flr(x / 8);
                                    int my = flr(y / 8);
                                    var grid_pos = inst.map_pos_to_meta_tile(mx, my);

                                    for (int i = 0; i <= 1; i++)
                                    {
                                        for (int j = 0; j <= 1; j++)
                                        {
                                            var final_x = (grid_pos.X + i) * 8 + 4;
                                            var final_y = (grid_pos.Y + j) * 8 + 4;
                                            inst.objs_add_queue.Add(
                                                new simple_fx_particle(-1 + (i * 2), (1 - j + 1) * -3, (8 + i) + ((52 + j) * 16))
                                                {
                                                    x = final_x,
                                                    y = final_y,
                                                });
                                        }
                                    }

                                    has_rock_armor = false;
                                }

                                if (flying != null || had_rock_armor)
                                {
                                    Vector2 pos = new Vector2(x, y);
                                    inst.pc.pawn.start_dash_bounce(ref pos);

                                    // Move the the edge of the bad guy to avoid hitting the next frame.
                                    float delta_x = inst.pc.pawn.x - x;
                                    inst.pc.pawn.x = x + Math.Sign(delta_x) * (cw * 0.5f + inst.pc.pawn.cw * 0.5f);

                                }
                            }
                            else if (cy > player_bottom)
                            {
                                if (inst.pc.pawn.dy >= 0 && !touch_damage)
                                {
                                    on_stomp();
                                }
                            }
                            else
                            {
                                //self:on_attack(p1);
                                inst.pc.pawn.on_take_hit(this);
                            }
                        }
                    }
                }
               
                base._update60();
            }

            public override void _draw()
            {
                base._draw();

                if (has_rock_armor)
                {
                    spr(840, x - 8, y - 8, 2, 2);
                }
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

            public void on_launch()
            {
                dy = -3;
                cleared_attacker = false;
                launched = true;
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
                inst.pc.pawn.dy = inst.pc.pawn.max_dy * amount;
            }
        }

        public class chopper_body : badguy
        {
            public chopper_body() : base(0)
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
            public chopper() : base(0)
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

                inst.objs_add_queue.Add(new chopper_body()
                {
                    x = x,
                    y = y + 4,
                    flipx = flipx,
                    dx = 0,
                    dy = 0,
                });
                inst.objs_add_queue.Add(new simple_fx_rotor()
                {
                    x = x,
                    y = y - 8,
                    flipx = flipx,
                    dx = 0,
                    dy = -0.5f,
                });
                inst.objs_remove_queue.Add(this);
            }
        }

        public class lava_splash : badguy
        {
            public lava_splash(float dir) : base(dir)
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

                touch_damage = true;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs_remove_queue.Add(this);
                };
            }

            public override void _update60()
            {
                base._update60();

                //if (curframe == 1)
                //{
                //    touch_damage = true;
                //}
                //else
                //{
                //    touch_damage = false;
                //}
            }
        }

        public class steam_spawner : sprite
        {
            int ticks = 0;

            public steam_spawner()
            {

            }

            public override void _update60()
            {
                base._update60();

                ticks++;

                if (ticks % 60 == 0)
                {
                    inst.objs_add_queue.Add(new steam_splash() { x = x, y = y });
                }
            }
        }

        public class steam_splash : badguy
        {
            public steam_splash() : base(0)
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
                                create_anim_frame(368, 2, 2),
                                create_anim_frame(371, 2, 2),
                                create_anim_frame(374, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 8;
                ch = 8;
                cy_offset = 4;

                dx = 0;

                touch_damage = true;
                attack_power = 0.25f;

                event_on_anim_done += delegate (string anim_name)
                {
                    inst.objs_remove_queue.Add(this);
                };
            }

            public override void _update60()
            {
                base._update60();
            }
        }

        public class water_splash : simple_fx
        {
            public water_splash()
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
                                create_anim_frame(368, 2, 2),
                                create_anim_frame(371, 2, 2),
                                create_anim_frame(374, 2, 2),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 16;
                h = 16;
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
                        inst.objs_remove_queue.Add(this);
                        return;
                    }
                    inst.objs_add_queue.Add(new lava_splash(dir) { x = x, y = y, /*, dx = rnd(1) * dir*/});
                }


                base._update60();
            }
        }

        public class lava_blaster : badguy
        {
            int idle_ticks = 0;

            public lava_blaster(float dir) : base(dir)
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

                has_rock_armor = true;

                event_on_anim_done += delegate (string anim_name)
                {
                    if (dead_time > 0)
                    {
                        return;
                    }
                    if (anim_name == "open_mouth")
                    {
                        inst.objs_add_queue.Add(new lava_blast_spawner(flipx ? -1 : 1) { x = x, y = y + 4 });
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
                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p.pawn);

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
                    inst.pc.pawn.x += self.x - old_x;
                    inst.pc.pawn.y += self.y - old_y;

                    //inst.p.pawn.dx = self.x - old_x;
                    //inst.p.pawn.dy = self.y - old_y;

                    //inst.p.pawn.platformed = true;
                }
                else
                {
                    inst.pc.pawn.platformed = false;
                }

                //if (tick >= 0.5f)
                //{
                //    inst.change_meta_tile(flr(x / 8), flr(y / 8), new int[] { 868, 869, 884, 885 });
                //    inst.objs.Remove(this);
                //}

                // Should be handled by collide side.
                //if (inst.p.pawn.dash_time > 0 && inst.intersects_obj_obj(this, inst.p.pawn))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.pawn.x) * 0.5f, y - (y - inst.p.pawn.y) * 0.5f);
                //    inst.p.pawn.start_dash_bounce(ref v);
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
                var touching_player = inst.intersects_box_box(inst.pc.pawn.cx, inst.pc.pawn.cy + inst.pc.pawn.ch * 0.5f, inst.pc.pawn.cw * 0.5f, 1, self.cx, self.cy, self.cw * 0.5f, (self.ch + 2) * 0.5f);
                //var touching_player = inst.intersects_obj_obj(self, inst.p.pawn);

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
                    inst.pc.pawn.x += self.x - old_x;
                    inst.pc.pawn.y += self.y - old_y;

                    //inst.p.pawn.dx = self.x - old_x;
                    //inst.p.pawn.dy = self.y - old_y;

                    //inst.p.pawn.platformed = true;
                }
                else
                {
                    inst.pc.pawn.platformed = false;
                }

                if (tick >= 0.5f)
                {
                    inst.change_meta_tile(flr(x/8), flr(y/8), new int[] { 868, 869, 884, 885 });
                    inst.objs_remove_queue.Add(this);
                }

                // Should be handled by collide side.
                //if (inst.p.pawn.dash_time > 0 && inst.intersects_obj_obj(this, inst.p.pawn))
                //{
                //    Vector2 v = new Vector2(x - (x - inst.p.pawn.x) * 0.5f, y - (y - inst.p.pawn.y) * 0.5f);
                //    inst.p.pawn.start_dash_bounce(ref v);
                //}
            }
        }

        public class player_top : player_pawn
        {
            public float dest_x = 0;
            public float dest_y = 0;

            public float walk_speed = 1.0f;

            public Vector2 desired_dir = Vector2.Zero;

            public player_top()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "walk_down",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(448, 2, 2),
                                create_anim_frame(480, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_left",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(450, 2, 2),
                                create_anim_frame(482, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_up",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(452, 2, 2),
                                create_anim_frame(484, 2, 2),
                            }
                        }
                    },
                    {
                        "walk_right",
                        new anim()
                        {
                            loop = true,
                            ticks=15,//how long is each frame shown.
                            //frames= new int[][] { new int[] { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35 } },//what frames are shown.
                            frames = new int[][]
                            {
                                create_anim_frame(454, 2, 2),
                                create_anim_frame(486, 2, 2),
                            }
                        }
                    },
                };

                set_anim("walk_down");
                
                w = 16;
                h = 16;

                event_on_anim_done = null;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.hit_pause.is_paused() || (inst.cur_game_state == game_state.gameplay_dead))
                {
                    return;
                }

                // Calculate prior to moving so that if we reach the
                // destination, we still remember what direction we
                // we going.

                if (dest_x != x)
                {
                    float delta = dest_x - x;
                    float dir = Math.Sign(delta);
                    x += dir * min(walk_speed, abs(delta));
                    if (dir > 0)
                    {
                        set_anim("walk_right");
                    }
                    else
                    {
                        set_anim("walk_left");
                    }
                }
                if (dest_y != y)
                {
                    float delta = dest_y - y;
                    float dir = Math.Sign(delta);
                    y += dir * min(walk_speed, abs(delta));
                    if (dir > 0)
                    {
                        set_anim("walk_down");
                    }
                    else
                    {
                        set_anim("walk_up");
                    }
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

        public class player_controller : sprite
        {
            public player_pawn pawn { get; protected set; }
            
            public artifacts found_artifacts = artifacts.none;// artifacts.health_00 | artifacts.dash_pack | artifacts.jump_boots;

            // Iinitial implementation supports 8 levels with up to 4 gems on 
            // each.
            // TODO: Add ability to dynamically calculate how many gems are in game.
            // TODO: Expand to support more levels.
            // TODO: Possibly expand the support variable numbers of gems per level.
            public UInt32 found_gems;

            public player_controller()
            {
                reload();
            }

            public void reload()
            {
                found_gems = (uint)dget((uint)Game1.cartdata_index.gems);
                found_artifacts = (artifacts)dget((uint)Game1.cartdata_index.artifacts);
            }

            public override void _update60()
            {
                base._update60();

                pawn._update60();
            }

            public override void _draw()
            {
                base._draw();

                pawn._draw();
            }
            
            public virtual void possess(player_pawn p)
            {
                p.set_controller(this);

                // Copy over important stuff between the pawns.
                if (pawn != null && pawn.hp > 0)
                {
                    p.hp = pawn.hp;
                    //p.curanim = pawn.curanim;
                    //p.curframe = pawn.curframe;
                    //p.flipx = pawn.flipx;
                    //p.dx = pawn.dx;
                    //p.dy = pawn.dy;

                    // If the new pawn is the same type as the old type, then re-use it
                    // as this is just a simple level transition.
                    if (pawn != null && p.GetType() == pawn.GetType())
                    {
                        pawn.x = p.x;
                        pawn.y = p.y;
                    }
                    else
                    {
                        pawn = p;
                    }
                }
                else
                {
                    // if this is the first pawn, give them full health.
                    // or if the current pawn is dead (we assume this means we are respawning).
                    p.hp = p.get_hp_max();
                    pawn = p;
                }
            }
        }

        [Flags]
        public enum artifacts : Int32
        {
            none            = 0,

            // Health pieces
            health_00       = 1 << 0,
            health_01       = 1 << 1,
            health_02       = 1 << 2,
            health_03       = 1 << 3,
            health_start    = health_00,
            health_end      = health_03,

            // Human tech
            dash_pack       = 1 << 4,
            jump_boots      = 1 << 5,
            rock_smasher    = 1 << 6,

            MAX =  1 << 31, // Just here as a reminder that this bitmask must remain 32 bit.

            // Alien relics
        }

        public class player_pawn : sprite
        {
            protected player_controller controller;

            public bool platformed = false;
            public float max_dx = 1;//max x speed
            public float max_dy = 4;//max y speed

            // Hack to solve case where player hits 2 flying enemies in the same frame.
            // First enemy starts playing dash bouncing, and second sees he isn't jumping
            // and does damage.
            // Note: I think a better, more general solution, would be to make player invul
            //       for a short time after dash bouncing, or possibly even let them attack
            //       during that time.
            public bool dashing_last_frame { get; protected set; }

            public override int get_hp_max()
            {
                int health = 1;

                for (int i = (int)artifacts.health_start; i <= (int)artifacts.health_end; i = i << 1)
                {
                    if ((controller.found_artifacts & (artifacts)i) != 0)
                    {
                        health++;
                    }
                }
                return health;
            }

            public virtual bool get_is_dashing()
            {
                return false;
            }

            public virtual void start_dash_bounce(ref Vector2 hit_point)
            {
            }

            public virtual void on_take_hit(sprite attacker)
            {

            }

            // Should only be called by PC, so that relationship is maintained.
            public virtual void set_controller(player_controller c)
            {
                controller = c;
            }
        }

        //make the player
        public class player_side : player_pawn
        {
            //todo: refactor with m_vec.

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

            int jump_count = 0;

            bool in_water = false;

            // The direction of the current dash. If not dashing,
            // this will be zero.
            Vector2 dash_dir = Vector2.Zero;

            public player_side() : base()
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
                        "dash_down",
                        new anim()
                        {
                            h =32,
                            ticks=5,//how long is each frame shown.
                            frames= new int[][] 
                            {
                                create_anim_frame(332, 4, 4),
                                create_anim_frame(396, 4, 4),
                            },
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
            }

            public override void start_dash_bounce(ref Vector2 hit_point)
            {
                // Clear out the dash direction since we hit a surface of something.
                dash_dir = Vector2.Zero;
                dy = -8;
                dx = 5 * -Math.Sign(hit_point.X - cx);
                dash_time = 0;
                dash_count = 0;
                inst.objs_add_queue.Add(new simple_fx() { x = hit_point.X, y = y + h * 0.25f });

                int mx = flr(hit_point.X / 8.0f);
                int my = flr(hit_point.Y / 8.0f);
                if (fget(mget(mx, my), 2))
                {
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 });
                    inst.objs_add_queue.Add(new block_restorer(mx, my, 240));
                }
                if (fget(mget(mx, my), 3))
                {
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 });
                    Point map_point = inst.map_pos_to_meta_tile(mx, my);
                    inst.objs_add_queue.Add(new rock_pendulum() { x = map_point.X * 8 + 8, y = map_point.Y * 8 + 8 });
                }
                if (fget(mget(mx, my), 5) && (controller.found_artifacts & artifacts.rock_smasher) != 0)
                {
                    var grid_pos = inst.map_pos_to_meta_tile(mx, my);

                    for (int i = 0; i <= 1; i++)
                    {
                        for (int j = 0; j <= 1; j++)
                        {
                            var final_x = (grid_pos.X + i) * 8 + 4;
                            var final_y = (grid_pos.Y + j) * 8 + 4;
                            inst.objs_add_queue.Add(
                                new simple_fx_particle(-1 + (i * 2), (1 - j + 1) * -3, mget(final_x / 8, final_y / 8))
                                {
                                    x = final_x,
                                    y = final_y,
                                });
                        }
                    }
                    inst.change_meta_tile(mx, my, new int[] { 836, 837, 852, 853 });
                }

                inst.hit_pause.start_pause(hit_pause_manager.pause_reason.bounce);
            }

            public override bool get_is_dashing()
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
                var bu = btn(2); //up
                var bd = btn(3); //down
                dash_button._update60();

                if (dash_button.is_pressed && dash_count == 0 && dash_time <= 0 && (controller.found_artifacts & artifacts.dash_pack) != 0)
                {
                    dash_count = 1;
                    dash_time = 30;
                    dy = 0;
                    self.jump_hold_time = 0; // kill jump

                    // Assume not direction to start.
                    dash_dir = Vector2.Zero;

                    // First check up and down to let that take
                    // presendence, since that feels better.
                    //if (bu)
                    //{
                    //    dash_dir.Y = -1;
                    //    // Clear the x momentum.
                    //    dx = 0;
                    //}
                    //else 
                    if (bd)
                    {
                        dash_dir.Y = 1;
                        dx = 0;
                    }
                    else if (br)
                    {
                        self.flipx = false;
                        dash_dir.X = 1;
                    }
                    else if (bl)
                    {
                        self.flipx = true;
                        dash_dir.X = -1;
                    }
                    else
                    {
                        // If no direction is being pressed but we are starting a
                        // dash, fall back to the currently looking direction of the
                        // player.
                        if (self.flipx)
                        {
                            dash_dir.X = -1;
                        }
                        else
                        {
                            dash_dir.X = 1;
                        }
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
                            if (self.grounded != 0)
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

                // No expiry for down dash. It feels better this way since
                // falling 
                if (dash_dir.Y <= 0)
                {
                    dash_time = max(0, dash_time - 1);
                }

                const float dash_speed = 2.0f;
                if (is_dashing)
                {
                    if (dash_dir.X < 0)
                    {
                        dx = -dash_speed;
                    }
                    else if (dash_dir.X > 0)
                    {
                        dx = dash_speed;
                    }
                    if (dash_dir.Y < 0)
                    {
                        dy = -dash_speed;
                    }
                    else if (dash_dir.Y > 0)
                    {
                        // ground pound is faster.
                        dy = dash_speed * 4;
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
                    if (is_dashing && dash_dir.X != 0)
                    {
                        start_dash_bounce(ref hit_point);
                        is_dashing = false;
                    }

                }

                //jump buttons
                self.jump_button._update60();

                bool in_water_new = (fget(mget(flr(x / 8), flr(y / 8)), 4));

                if (!in_water && in_water_new)
                {
                    inst.objs.Add(new water_splash()
                    {
                        x = x,
                        y = flr(y/16) * 16.0f - 8.0f,
                    });
                }

                in_water = in_water_new;

                //jump is complex.
                //we allow jump if:
                //	on ground
                //	recently on ground
                //	pressed btn right before landing
                //also, jump velocity is
                //not instant. it applies over
                //multiple frames.
                //if (!is_dashing)

                int mod_max_jump_press = max_jump_press; // in_water ? max_jump_press * 6 : max_jump_press;
                float mod_jump_speed = in_water ? jump_speed * 2.0f : jump_speed;

                {
                    if (self.jump_button.is_down)
                    {
                        //is player on ground recently.
                        //allow for jump right after 
                        //walking off ledge.
                        var on_ground = (self.grounded != 0 || self.airtime < 5);
                        //was btn presses recently?
                        //allow for pressing right before
                        //hitting ground.
                        var new_jump_btn = self.jump_button.ticks_down < 10;

                        int max_jump_count = ((controller.found_artifacts & artifacts.jump_boots) != 0) ? 2 : 1;
                        //is player continuing a jump
                        //or starting a new one?
                        if (self.jump_hold_time > 0)
                        {
                            self.jump_hold_time += 1;
                            //keep applying jump velocity
                            //until max jump time.
                            if (self.jump_hold_time < mod_max_jump_press)
                            {
                                self.dy = mod_jump_speed;//keep going up while held
                            }

                            dash_time = 0;
                            is_dashing = false;
                        }
                        else if ((on_ground && new_jump_btn && jump_count == 0))
                        {
                            // Are we jumping off a pass through floor with "down" held?
                            if ((grounded & (1 << 6)) != 0 && btn(3))
                            {
                                // Teleport below the floor to avoid collision.
                                y += 8;

                                // Artifically increment airtime to avoid hitting 
                                // "late jump" code in "on_ground" calculation above here.
                                self.airtime = 5;
                            }
                            else
                            {
                                jump_hold_time += 1;
                                jump_count++;
                                dash_time = 0;
                                is_dashing = false;
                                self.dy = mod_jump_speed;
                            }
                        }
                        else if (jump_count < max_jump_count && is_dashing && jump_button.is_pressed)
                        {
                            // Additional jumps get a little effect to show that it is special
                            inst.objs_add_queue.Add(new simple_fx() { x = x, y = y + h * 0.5f });
                            jump_hold_time += 1;
                            jump_count++;
                            dash_time = 0;
                            is_dashing = false;
                            self.dy = mod_jump_speed;

                            // Starting a dash out, so dash is cancelled.
                            dash_dir = Vector2.Zero;
                        }
                    }
                    else
                    {
                        if (jump_button.is_released && (self.jump_hold_time > 0 && self.jump_hold_time < mod_max_jump_press))
                        {
                            self.dy = -1.0f;
                        }

                        self.jump_hold_time = 0;
                    }

                    float grav_mod = in_water ? 0.5f : 1.0f;

                    //move in y
                    if (dash_dir.Y == 0)
                    {
                        self.dy += self.grav * grav_mod;
                    }
                }

                self.dy = mid(-self.max_dy, self.dy, self.max_dy);

                // Apply gravity if not dashing, or vertical dash.
                if (!is_dashing || dash_dir.Y != 0) // re-eval is_dashing since we might have just started jumping.
                {
                    self.y += self.dy;
                }
                else if (dash_dir.Y == 0) // is horizontal dashing.
                {
                    self.dy = 0; // kill building pull down
                }

                //floor
                if (!inst.collide_floor(self, out hit_point))
                {
                    next_anim = ("jump");

                    self.grounded = 0;
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
                    // Are we downward dashing?
                    if (dash_dir.Y > 0 & is_dashing)
                    {
                        inst.game_cam.shake(10, 3);
                        start_dash_bounce(ref hit_point);

                        // hack. Should probably be distance based.
                        foreach(var o in inst.objs)
                        {
                            if (o.GetType() == typeof(badguy))
                            {
                                if ((o as badguy).grounded != 0)
                                {
                                    (o as badguy).on_launch();
                                }
                            }
                        }
                    }
                }

                //roof
                if (inst.collide_roof(self))
                {
                    // TODO: For now just kill dash. Should probably do some of 
                    // start_dash_bounce() to trigger tiles changes etc, but
                    // not the bouncing part.
                    dash_dir = Vector2.Zero;
                    dash_time = 0;
                    dy = 0;
                }
                
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
                if (self.grounded != 0 && !is_dashing)
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
                    if (grounded != 0)
                    {
                        next_anim = ("dash");
                    }
                    else
                    {
                        if (dash_dir.Y != 0)
                        {
                            next_anim = "dash_down";
                        }
                        else
                        {
                            next_anim = "dash_air";
                        }
                    }
                }

                // Has the dash expired?
                if (dash_time <= 0)
                {
                    // We are no longer dashing in any direction.
                    dash_dir = Vector2.Zero;

                    // If we are also on the ground, reset the dash count, so
                    // the user can dash again.
                    if (grounded != 0)
                    {
                        dash_count = 0;
                    }
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

            public override void on_take_hit(sprite attacker)
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
                        inst.objs_add_queue.Add(o);
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

            player_controller tar;//target to follow.
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

            public cam(player_controller target)
            {
                tar = target;
                jump_to_target();
            }
            public void jump_to_target()
            {
                pos = new Vector2(tar.pawn.x, tar.pawn.y);
            }
            public override void _update60()
            {
                var self = this;

                base._update60();

                self.shake_remaining = (int)max(0, self.shake_remaining - 1);

                //follow target outside of
                //pull range.
                if (pull_max_x() < self.tar.pawn.x)
                {

                    self.pos.X += min(self.tar.pawn.x - pull_max_x(), 4);

                }
                if (pull_min_x() > self.tar.pawn.x)
                {
                    self.pos.X += min((self.tar.pawn.x - pull_min_x()), 4);
                }


                if (pull_max_y() < self.tar.pawn.y)
                {
                    self.pos.Y += min(self.tar.pawn.y - pull_max_y(), 4);

                }
                if (pull_min_y() > self.tar.pawn.y)
                {
                    self.pos.Y += min((self.tar.pawn.y - pull_min_y()), 4);

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

            public void shake(int ticks, float force)
            {
                shake_remaining = ticks;

                shake_force = force;
            }

            public bool is_obj_off_screen(sprite s)
            {
                return !inst.intersects_obj_box(s, pos.X, pos.Y, inst.Res.X * 0.5f, inst.Res.Y * 0.5f);
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
                    if (self == pc.pawn && v.is_platform)
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
        bool collide_floor(sprite self, out Vector2 hit_point)
        {
            //didn't hit anything solid.
            hit_point.X = 0;
            hit_point.Y = 0;

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

            byte collision_flag = 0;

            for (float i = -(offset_x); i <= (offset_x); i += 2)
            {
                var box_x = self.cx;
                var box_y = self.cy;
                var box_w_half = self.cw * 0.5f;
                var box_h_half = self.ch * 0.5f;


                var y = flr((box_y + box_h_half) / 8);
                var y_actual = flr((self.y + box_h_half) / 8);
                byte tile_flag = fget(mget(flr((box_x + i) / 8), y));
                if ((tile_flag & (1 << 0 | 1 << 6)) != 0)
                {
                    new_y = (flr(y) * 8) - box_h_half + (self.y - self.cy);
                    collision_flag = tile_flag;

                    hit_point.X = self.cx + i;
                    hit_point.Y = self.cy + offset_y;

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
                        if (self == pc.pawn && v.is_platform)
                        {
                            // Check a 1 pixel high box along the bottom the the player.
                            // Adding 2 to the solid because that is what solids do in their update to stick to
                            // objects when moving away from them.
                            if (inst.intersects_box_box(self.cx, self.cy + self.ch * 0.5f, self.cw * 0.5f, 1, v.cx, v.cy, v.cw * 0.5f, (v.ch + 2) * 0.5f))
                            {
                                new_y = (/*flr*/(v.cy - v.ch * 0.5f)) - (self.ch * 0.5f) - (self.cy_offset);
                                // fake standard collision.
                                collision_flag = 1 << 0;

                                // We don't really know the hit point, so just put it at the center on the edge that hit.
                                hit_point.X = self.cx;
                                hit_point.Y = self.cy + offset_y;

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
                self.grounded = collision_flag;
                self.airtime = 0;
                return true;
            }

            if (self.stay_on && !self.launched)
            {
                self.dx *= -1;
                self.x += self.dx;
                // fake collision flag
                collision_flag = 1 << 0;
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
                        if (self == pc.pawn && v.is_platform)
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
                artifact_picked_up,
                gem_picked_up,

                message_box_open,
                level_trans,
            }

            Dictionary<pause_reason, int> pause_times = new Dictionary<pause_reason, int>()
            {
                { pause_reason.bounce, 0 }, // no pause for now. happens too much.
                { pause_reason.death, 30 },
                { pause_reason.artifact_picked_up, 30 },
                { pause_reason.gem_picked_up, 0 },
                { pause_reason.message_box_open, 1 },
                { pause_reason.level_trans, 1 },
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
                    inst.objs_remove_queue.Add(this);
                }
            }
        }

        public class map_link : sprite
        {
            public enum transition_dir
            {
                vert,
                horz,
                none,
            };

            public string dest_map_path;
            public string dest_spawn_name;
            public transition_dir trans_dir;

            public map_link()
            {
                trans_dir = transition_dir.none;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    inst.active_map_link = this;
                    inst.queued_map = dest_map_path;
                    // Store the offset relative to the map link, so that on the other side of the transition,
                    // we can offset the same amount.
                    //inst.spawn_offset.X = inst.pc.pawn.x - inst.game_cam.cam_pos().X;
                    //inst.spawn_offset.Y = inst.pc.pawn.y - inst.game_cam.cam_pos().Y;
                }
            }

            public override void _draw()
            {
                // do nothing.
                base._draw();
            }
        }

        public class gem_pickup : sprite
        {
            // Index within the level is resides in.
            public int id;

            public gem_pickup(int id)
            {
                    anims = new Dictionary<string, anim>()
                    {
                        {
                            "default",
                            new anim()
                            {
                                ticks=15,//how long is each frame shown.
                                frames = new int[][]
                                {
                                    create_anim_frame(292, 2, 2),
                                    create_anim_frame(262, 2, 2),
                                    create_anim_frame(264, 2, 2),
                                    create_anim_frame(266, 2, 2),
                                }
                            }
                        },
                    };

                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;

                this.id = id;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(inst.pc.pawn, this))
                {
                    UInt32 gem_mask = (UInt32)1 << id + (inst.gems_per_level * inst.cur_level_id);

                    inst.pc.found_gems |= gem_mask;

//                    dset((uint)Game1.cartdata_index.gems, (int)inst.pc.found_gems);

                    inst.objs_remove_queue.Add(this);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.gem_picked_up);
                    return;
                }
            }

            public override void _draw()
            {
                base._draw();

                //print(id.ToString(), x, y, 8);
            }
        }

        public class artifact_pickup : sprite
        {
            artifacts id;

            public artifact_pickup(artifacts id)
            {
                this.id = id;

                if (id >= artifacts.health_start && id <= artifacts.health_end)
                {
                    anims = new Dictionary<string, anim>()
                            {
                                {
                                    "default",
                                    new anim()
                                    {
                                        ticks=10,//how long is each frame shown.
                                        frames = new int[][]
                                        {
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(300, 2, 2),
                                            create_anim_frame(302, 2, 2),
                                        }
                                    }
                                },
                            };
                }
                else
                {
                    anims = new Dictionary<string, anim>()
                            {
                                {
                                    "default",
                                    new anim()
                                    {
                                        ticks=30,//how long is each frame shown.
                                        frames = new int[][]
                                        {
                                            create_anim_frame(268, 2, 2),
                                            create_anim_frame(270, 2, 2),
                                        }
                                    }
                                },
                            };
                }
                
                set_anim("default");

                w = 16;
                h = 16;
                cw = 16;
                ch = 16;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(inst.pc.pawn, this))
                {
                    inst.pc.found_artifacts |= id;

                    //dset((uint)Game1.cartdata_index.artifacts, (int)inst.pc.found_artifacts);

                    if (id >= artifacts.health_start && id <= artifacts.health_end)
                    {
                        inst.pc.pawn.hp = inst.pc.pawn.get_hp_max();
                    }
                    inst.objs_remove_queue.Add(this);
                    inst.hit_pause.start_pause(hit_pause_manager.pause_reason.artifact_picked_up);

                    string display_name = "";

                    switch(id)
                    {
                        case artifacts.dash_pack:
                            {
                                display_name = "dash pack";
                                break;
                            }
                        case artifacts.jump_boots:
                            {
                                display_name = "jump boots";
                                break;
                            }

                        case artifacts.rock_smasher:
                            {
                                display_name = "rock smasher";
                                break;
                            }

                        default:
                            {
                                display_name = "heart container";
                                break;
                            }
                    }
                    inst.message = new message_box();
                    inst.message.set_message("the title", display_name + " acquired!");

                    return;
                }
            }
        }

        public class rocket_ship : sprite
        {
            bool hit = false;
            public rocket_ship()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "default",
                        new anim()
                        {
                            ticks=15,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(296, 4, 4),
                            }
                        }
                    },
                };

                set_anim("default");

                w = 32;
                h = 32;
                cw = 16;
                ch = 16;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    if (hit == false)
                    {

                        // todo: count gems found.
                        // temp hack. 1111 0000 is all found gems on level id 1 (pit with water).
                        if (inst.pc.found_gems >= 0xf0)
                        {
                            inst.message = new message_box();
                            inst.message.set_message("title", "ship powers up, and lift off!", () => { inst.set_game_state(game_state.game_win); } );
                        }
                        else
                        {
                            inst.message = new message_box();
                            inst.message.set_message("title", "more goodies needed!");
                        }

                    }
                    hit = true;
                }
                else
                {
                    hit = false;
                }
            }
        }

        // TODO:
        // * Trigger save when entering checkpoint.
        // * Show as active when returning to the map with the last active checkpoint.
        // * Save the last used checkpoint, and load into that map when resuming game.
        public class checkpoint : sprite
        {
            // Is this the currently active checkpoint.
            public bool is_activated { get; private set; }

            // The name of the map where this checkpoint spawned.
            public string map_name;

            public bool touching;

            public checkpoint()
            {
                anims = new Dictionary<string, anim>()
                {
                    {
                        "off",
                        new anim()
                        {
                            ticks=30,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(524, 4, 4),
                            }
                        }
                    },
                    {
                        "on",
                        new anim()
                        {
                            ticks=10,//how long is each frame shown.
                            frames = new int[][]
                            {
                                create_anim_frame(512, 4, 4),
                                create_anim_frame(516, 4, 4),
                                create_anim_frame(520, 4, 4),
                            }
                        }
                    },
                };

                is_activated = false;

                set_anim("off");

                w = 32;
                h = 32;
                cw = 16;
                ch = 16;

                touching = false;
            }

            public override void _update60()
            {
                base._update60();

                if (inst.intersects_obj_obj(this, inst.pc.pawn))
                {
                    if (!touching)
                    {
                        dset((uint)Game1.cartdata_index.gems, (int)inst.pc.found_gems);
                        dset((uint)Game1.cartdata_index.artifacts, (int)inst.pc.found_artifacts);

                        if (!is_activated)
                        {
                            // Deactive the currently active one.
                            if (inst.last_activated_checkpoint != null)
                            {
                                inst.last_activated_checkpoint.deactivate();
                            }

                            inst.last_activated_checkpoint = this;
                            map_name = inst.current_map;
                            is_activated = true;
                            set_anim("on");

                            // Check if any other checkpoints are active on the map, and
                            // deactivate them.
                            //foreach (PicoXObj o in inst.objs)
                            //{
                            //    checkpoint c = o as checkpoint;
                            //    if (c != null && c != this)
                            //    {
                            //        c.deactivate();
                            //    }
                            //}
                        }
                    }
                    touching = true;
                }
                else
                {
                    touching = false;
                }
            }

            public void deactivate()
            {
                is_activated = false;
                set_anim("off");
            }
        }

        public enum game_state
        {
            main_menu,
            level_trans_exit,
            level_trans_enter,
            gameplay,
            gameplay_dead,
            game_over,
            game_win,
        }

        player_controller pc;
        cam game_cam;

        List<PicoXObj> objs;
        List<PicoXObj> objs_remove_queue;
        List<PicoXObj> objs_add_queue;

        game_state cur_game_state;
        uint time_in_state;
        complex_button start_game;
        int cur_map_bank = 0;

        public hit_pause_manager hit_pause;

        public string current_map   = "Content/raw/test_map_hall_left.tmx";
        public string queued_map    = "Content/raw/test_map_hall_left.tmx";
        public map_link active_map_link;

        public int cur_level_id = 0;
        public int gems_per_level = 4;

        public checkpoint last_activated_checkpoint;

        int level_trans_time = 10;

        public class message_box
        {
            public int chars_per_line;

            public string title { get; private set; }

            public Action on_close_delegate;

            public void set_message(string title, string body, Action on_close = null)
            {
                body_with_breaks = new List<string>();

                chars_per_line = 32;

                string[] words = body.Split(' ');
                string line = "";

                for (int i = 0; i < words.Length; i++)
                {
                    // TODO: if a single word is longer than the single line, then add hyphen and move to new line.
                    if (words[i].Length + line.Length < chars_per_line)
                    {
                        line += words[i] + " ";
                    }
                    else
                    {
                        body_with_breaks.Add(line);
                        line = words[i] + " ";
                    }
                }

                // Add the last line.
                // TODO: Handle case where the last word triggered an Add already.
                body_with_breaks.Add(line);

                on_close_delegate = on_close;
            }

            public List<string> body_with_breaks { get; private set; }
        };

        public message_box message;

        // save game index info.
        public enum cartdata_index : uint
        {
            version = 0,
            gems = 1,
            artifacts = 2,
        }

        public Game1() : base()
        {
            // MUST BE DONE BEFORE ANY PICOXOBJ ARE CREATED
            inst = this;
        }

        public void set_game_state(game_state new_state)
        {
            // Used in the case of entering gameplay, both from transitioning maps,
            // and flow between states.
            Vector2 spawn_point = Vector2.Zero;

            // Leaving...
            switch (cur_game_state)
            {
                case game_state.gameplay_dead:
                    {
                        if (new_state == game_state.game_over)
                        {
                            objs.Clear();
                            objs_remove_queue.Clear();
                            objs_add_queue.Clear();
                        }
                        break;
                    }

                case game_state.game_over:
                    {
                        // If we died we want to reload the PC to the previous state of the save game.
                        // Don't want to do this in 'entering...' section because that will get hit when
                        // moving between levels.
                        pc.reload();

                        if (new_state == game_state.level_trans_enter)
                        {
                            if (last_activated_checkpoint != null)
                            {
                                current_map = last_activated_checkpoint.map_name;
                                queued_map = current_map;
                                spawn_point.X = last_activated_checkpoint.cx;
                                spawn_point.Y = last_activated_checkpoint.cy;
                            }
                        }
                        break;
                    }
            }

            cur_game_state = new_state;
            time_in_state = 0;

            // Entering...
            switch (cur_game_state)
            {
                case game_state.game_win:
                    {
                        objs.Clear();
                        objs_remove_queue.Clear();
                        objs_add_queue.Clear();
                        break;
                    }
                case game_state.level_trans_exit:
                    {
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        current_map = queued_map;

                        Vector2 cam_area_min = Vector2.Zero;
                        Vector2 cam_area_max = Vector2.Zero;

                        objs.Clear();
                        objs_remove_queue.Clear();
                        objs_add_queue.Clear();

                        reloadmap(GetMapString());

                        TmxMap TmxMapData = new TmxMap(GetMapString());

                        // Figure out what bank this map uses.
                        // NOTE: For now we assume that each map uses only 1 bank.
                        for (int i = 0; i < GetSheetPath().Count; i++)
                        {
                            string file_name = Path.GetFileNameWithoutExtension((TmxMapData.Tilesets[0]).Image.Source);
                            if (GetSheetPath()[i].EndsWith(file_name))
                            {
                                cur_map_bank = i;
                                break;
                            }
                        }

                        player_pawn pawn = null;

                        int gem_id = 0;

                        cur_level_id = int.Parse(TmxMapData.Properties["level_id"]);

                        foreach (var group in TmxMapData.ObjectGroups)
                        {
                            foreach (var o in group.Objects)
                            {
                                if (string.Compare(o.Type, "spawn_point", true) == 0)
                                {
                                    if (!string.IsNullOrEmpty(active_map_link?.dest_spawn_name))
                                    {
                                        if (active_map_link.dest_spawn_name != o.Name)
                                        {
                                            continue;
                                        }
                                    }
                                    // Account for the case of a checkpoint.
                                    if (spawn_point == Vector2.Zero)
                                    {
                                        spawn_point = new Vector2((float)o.X + ((float)o.Width * 0.5f), (float)o.Y + ((float)o.Height * 0.5f));
                                    }

                                    // mandatory field.
                                    switch (o.Properties["type"])
                                    {
                                        case "top":
                                            {
                                                pawn = new player_top()
                                                {
                                                    x = spawn_point.X,
                                                    y = spawn_point.Y,
                                                    w = 16,
                                                    h = 16,
                                                    cw = 16,
                                                    ch = 16,
                                                };
                                                (pawn as player_top).dest_x = pawn.x;
                                                (pawn as player_top).dest_y = pawn.y;

                                                break;
                                            }

                                        case "side":
                                            {
                                                pawn = new player_side()
                                                {
                                                    x = spawn_point.X,
                                                    y = spawn_point.Y,
                                                };
                                                break;
                                            }
                                    }
                                }
                                else if (string.Compare(o.Type, "cam_area", true) == 0)
                                {
                                    cam_area_min = new Vector2((float)o.X, (float)o.Y);
                                    cam_area_max = new Vector2((float)o.X + (float)o.Width, (float)o.Y + (float)o.Height);
                                }
                                else if (string.Compare(o.Type, "spawn_chopper", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new chopper()
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "spawn_lava_blaster", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new lava_blaster(Int32.Parse(o.Properties["dir"]))
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "spawn_steam_spawner", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new steam_spawner()
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "spawn_rolley", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new badguy(Int32.Parse(o.Properties["dir"]))
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "spawn_rocket_ship", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new rocket_ship()
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "spawn_checkpoint", true) == 0)
                                {
                                    objs_add_queue.Add(
                                            new checkpoint()
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            }
                                        );
                                }
                                else if (string.Compare(o.Type, "map_link", true) == 0)
                                {
                                    map_link ml = new map_link()
                                    {
                                        x = (float)o.X + ((float)o.Width * 0.5f),
                                        y = (float)o.Y + ((float)o.Height * 0.5f),
                                        w = (int)o.Width,
                                        h = (int)o.Height,
                                    };
                                    ml.cw = ml.w;
                                    ml.ch = ml.h;

                                    string out_string;
                                    if (o.Properties.TryGetValue("dest_map_path", out out_string))
                                    {
                                        ml.dest_map_path = out_string;
                                    }
                                    if (o.Properties.TryGetValue("dest_spawn_name", out out_string))
                                    {
                                        ml.dest_spawn_name = out_string;
                                    }
                                    if (o.Properties.TryGetValue("dir", out out_string))
                                    {
                                        ml.trans_dir = (map_link.transition_dir)Enum.Parse(typeof(map_link.transition_dir), out_string);
                                    }

                                    objs_add_queue.Add(ml);
                                }
                                else if (string.Compare(o.Type, "artifact", true) == 0)
                                {
                                    artifacts t = (artifacts)Enum.Parse(typeof(artifacts), o.Properties["id"]);

                                    // Has the player already found this?
                                    if ((pc.found_artifacts & t) == 0)
                                    {
                                        artifact_pickup ap = new artifact_pickup(t)
                                        {
                                            x = (float)o.X + ((float)o.Width * 0.5f),
                                            y = (float)o.Y + ((float)o.Height * 0.5f),
                                        };

                                        objs_add_queue.Add(ap);
                                    }
                                }
                                else if (string.Compare(o.Type, "gem", true) == 0)
                                {
                                    System.Diagnostics.Debug.Assert(cur_level_id >= 0);
                                    System.Diagnostics.Debug.Assert(gem_id < gems_per_level);

                                    // Max of 4 gems per level.
                                    if (gem_id < gems_per_level)
                                    {
                                        UInt32 gem_mask = (UInt32)1 << gem_id + (gems_per_level * cur_level_id);

                                        if ((inst.pc.found_gems & gem_mask) == 0)
                                        {
                                            gem_pickup gem = new gem_pickup(gem_id)
                                            {
                                                x = (float)o.X + ((float)o.Width * 0.5f),
                                                y = (float)o.Y + ((float)o.Height * 0.5f),
                                            };

                                            objs_add_queue.Add(gem);
                                        }

                                        gem_id++;
                                    }
                                }
                            }
                        }

                        //objs_add_queue.Add(new rock() { x = 37 * 8, y = 97 * 8, });
                        //objs_add_queue.Add(new rock() { x = 37 * 8, y = 89 * 8, });
                        //objs_add_queue.Add(new rock() { x = 27 * 8, y = 107 * 8, });
                        //for (int i = 0; i < 10; i++)
                        //{
                        //    objs_add_queue.Add(new badguy() { x = 27 * 8 + i * 16, y = 107 * 8 });
                        //}
                        //objs_add_queue.Add(new badguy() { x = 19 * 8, y = 97 * 8 });
                        //objs_add_queue.Add(new chopper() { x = 31 * 8, y = 85 * 8 });
                        //objs_add_queue.Add(new chopper() { x = 35 * 8, y = 80 * 8 });
                        //objs_add_queue.Add(new chopper() { x = 39 * 8, y = 75 * 8 });
                        //objs_add_queue.Add(new chopper() { x = 43 * 8, y = 70 * 8 });
                        //objs_add_queue.Add(new lava_splash() { x = 19 * 8, y = 97 * 8 });
                        //objs_add_queue.Add(new lava_blaster(1) { x = 9 * 8, y = 93 * 8 });
                        //objs_add_queue.Add(new lava_blaster(-1) { x = 40 * 8, y = 48 * 8 });
                        objs_add_queue.Add(pc);

                        pc.possess(pawn);

                        const int hud_height = 16;
                        game_cam = new cam(pc)
                        {
                            pos_min = cam_area_min + new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f - hud_height),
                            pos_max = cam_area_max - new Vector2(inst.Res.X * 0.5f, inst.Res.Y * 0.5f),
                        };
                        game_cam.jump_to_target();

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
            // Create save file.
            cartdata("mbh-platformer");

            // Zero's out all cartdata. Could do more complex logic if needed.
            Action clear_save = delegate ()
            {
                for (uint i = 0; i < 64; i++)
                {
                    dset(i, 0);
                }
            };

            // Add ability for user to clear save data.
            menuitem(1, "clear save", clear_save);

            int ver = dget((uint)cartdata_index.version);

            // If this is an old version, clear it.
            // Ideally this would not just clear the save but rather "upgrade it".
            if (ver < 1)
            {
                clear_save();
            }

            dset((uint)cartdata_index.version, 1);

            objs = new List<PicoXObj>();
            objs_remove_queue = new List<PicoXObj>();
            objs_add_queue = new List<PicoXObj>();
            // one player controller for the life of the game.
            pc = new player_controller();
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
                            set_game_state(game_state.level_trans_enter);
                        }
                        break;
                    }
                case game_state.level_trans_exit:
                    {
                        hit_pause.start_pause(hit_pause_manager.pause_reason.level_trans);
                        if (time_in_state >= level_trans_time * 2)
                        {
                            set_game_state(game_state.level_trans_enter);
                        }
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        hit_pause.start_pause(hit_pause_manager.pause_reason.level_trans);
                        if (time_in_state >= level_trans_time)
                        {
                            active_map_link = null;
                            set_game_state(game_state.gameplay);
                        }
                        break;
                    }
                case game_state.gameplay:
                    {
                        //if (time_in_state % 120 == 0)
                        //{
                        //    objs_add_queue.Add(new lava_blast_spawner() { x = 29 * 8, y = 93 * 8 });
                        //}
                        //if (time_in_state % 120 == 0)
                        //{
                        //    game_cam.shake((int)rnd(30) + 30, 1);
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
                            set_game_state(game_state.level_trans_enter);
                        }
                        break;
                    }
            }

            if (message != null)
            {
                if (btnp(4) || btnp(5))
                {
                    message.on_close_delegate?.Invoke();
                    message = null;
                }
                else
                {
                    hit_pause.start_pause(hit_pause_manager.pause_reason.message_box_open);
                }
            }

            // TODO: Should we ignore objects in the remove queue?
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

            // Remove all the objects which requested to be removed.
            objs = objs.Except(objs_remove_queue).ToList();
            objs_remove_queue.Clear();

            objs.AddRange(objs_add_queue);
            objs_add_queue.Clear();

            if (game_cam != null)
            {
                game_cam._update60();
            }
            if (hit_pause != null)
            {
                hit_pause._update60();
            }

            if (queued_map != GetMapString() && cur_game_state != game_state.level_trans_exit && cur_game_state != game_state.level_trans_enter)
            {
                set_game_state(game_state.level_trans_exit);
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
                Vector2 offset = Vector2.Zero;

                if (active_map_link != null && active_map_link.trans_dir != map_link.transition_dir.none)
                {
                    if (cur_game_state == game_state.level_trans_exit && time_in_state > level_trans_time)
                    {
                        float time = time_in_state - level_trans_time;
                        float amount = (float)(time) / (float)(level_trans_time);
                        if (active_map_link.trans_dir == map_link.transition_dir.horz)
                        {
                            amount *= Res.X;
                            if (pc.pawn.x < game_cam.cam_pos().X + Res.X * 0.5f)
                            {
                                amount *= -1.0f;
                            }
                            offset.X += amount;
                        }
                        else
                        {
                            amount *= Res.Y;
                            if (pc.pawn.y < game_cam.cam_pos().Y + Res.Y * 0.5f)
                            {
                                amount *= -1.0f;
                            }
                            offset.Y += amount;
                        }
                    }
                }
                camera(game_cam.cam_pos().X + offset.X, game_cam.cam_pos().Y + offset.Y);
            }
            else
            {
                camera(0, 0);
            }

            switch(cur_game_state)
            {
                case game_state.level_trans_exit:
                    {
                        int fade_step_time = level_trans_time / 3;
                        if (time_in_state < 0)
                        {

                        }
                        else if (time_in_state < fade_step_time)
                        {
                            pal(7, 6);
                            pal(6, 5);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        else if (time_in_state < fade_step_time * 2)
                        {
                            pal(7, 5);
                            pal(6, 0);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        else
                        {
                            pal(7, 0);
                            pal(6, 0);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        break;
                    }
                case game_state.level_trans_enter:
                    {
                        int fade_step_time = level_trans_time / 3;
                        if (time_in_state < fade_step_time)
                        {
                            pal(7, 0);
                            pal(6, 0);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        else if (time_in_state < fade_step_time * 2)
                        {
                            pal(7, 5);
                            pal(6, 0);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        else
                        {
                            pal(7, 6);
                            pal(6, 5);
                            pal(5, 0);
                            pal(0, 0);
                        }
                        break;
                    }
            }

            switch (cur_game_state)
            {
                case game_state.level_trans_exit:
                case game_state.level_trans_enter:
                case game_state.gameplay:
                case game_state.gameplay_dead:
                    {
                        //pal(7, 0);
                        //pal(6, 5);
                        //pal(5, 6);
                        //pal(0, 7);
                        bset(cur_map_bank);
                        map(0, 0, 0, 0, 16, 16);
                        bset(0);
                        //map(0, 0, 0, 0, 16, 16, 0, 1); // easy mode?
                        //pal();
                        break;
                    }
            }

            foreach (PicoXObj o in objs)
            {
                o._draw();
            }

            pal();

            // Draw the player here so that it draws over the fade out during level transition.
            if (pc.pawn != null)
            {
                pc.pawn._draw();
            }

            // HUD

            Action draw_health = () =>
            {
                if (pc == null || pc.pawn == null)
                {
                    return;
                }

                int y_pos = 1;

                for (int i = 0; i < pc.pawn.get_hp_max(); i++)
                {
                    int id = 238; // empty
                    if (i < flr(pc.pawn.hp))
                    {
                        id = 230;
                    }
                    else
                    {
                        float remainder = pc.pawn.hp - flr(pc.pawn.hp);
                        if (remainder > 0.75f)
                        {
                            id = 230; // full
                        }
                        else if (remainder > 0.5f)
                        {
                            id = 232; // 3/4
                        }
                        else if (remainder > 0.25f)
                        {
                            id = 234; // 2/4
                        }
                        else if (remainder > 0.0f)
                        {
                            id = 236; // 1/4
                        }
                        else
                        {
                            id = 238; // empty
                        }
                    }
                    spr(id, y_pos, 1, 2, 2);
                    y_pos += 16;
                }
            };

            camera(0, 0);

            int step = 1;

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
                        rectfill(0, 0, Res.X, 16, 7);
                        draw_health();

                        //if (time_in_state < 15)
                        //{
                        //    pal(7, 5, 1);
                        //    pal(6, 0, 1);
                        //    pal(5, 0, 1);
                        //    pal(0, 0, 1);
                        //}
                        //else if (time_in_state < 30)
                        //{
                        //    pal(7, 6, 1);
                        //    pal(6, 5, 1);
                        //    pal(5, 0, 1);
                        //    pal(0, 0, 1);
                        //}
                        /*
                        int length = 4 * 60;
                        int time_loop = (int)time_in_state % length;
                        int fade_step = 5;

                        if (time_loop < fade_step)
                        {
                            pal(5, 0, 1);
                            pal(6, 0, 1);
                            pal(7, 0, 1);
                        }
                        else if (time_loop < fade_step * 2)
                        {
                            pal(5, 0, 1);
                            pal(6, 0, 1);
                            pal(7, 5, 1);
                        }
                        else if (time_loop < fade_step * 3)
                        {
                            pal(5, 0, 1);
                            pal(6, 5, 1);
                            pal(7, 6, 1);
                        }
                        //else if (time_loop > length - fade_step)
                        //{
                        //    pal(5, 0, 1);
                        //    pal(6, 0, 1);
                        //    pal(7, 0, 1);
                        //}
                        else if (time_loop > length - fade_step * 1)
                        {
                            pal(5, 0, 1);
                            pal(6, 0, 1);
                            pal(7, 5, 1);
                        }
                        else if (time_loop > length - fade_step * 2)
                        {
                            pal(5, 0, 1);
                            pal(6, 5, 1);
                            pal(7, 6, 1);
                        }
                        */
                        break;
                    }
                case game_state.level_trans_exit:
                case game_state.level_trans_enter:
                    {
                        rectfill(0, 0, Res.X, 16, 7);
                        draw_health();
                        break;
                    }
                case game_state.gameplay_dead:
                    {
                        rectfill(0, 0, Res.X, 16, 7);
                        draw_health();

                        step = flr((time_in_state / 240.0f) * 32.0f);
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
                case game_state.game_win:
                    {
                        var str = "you win! the galaxy is at peace";
                        print(str, 128 - (str.Length * 0.5f) * 4, 120, 7);
                        break;
                    }
            }

            step = (int)max(step, 1);

            for (int x = 0; x < Res.X; x += step)
            {
                for (int y = 0; y < Res.Y; y += step)
                {
                    int color = pget(x, y);

                    for (int i = 0; i < step; i++)
                    {
                        for (int j = 0; j < step; j++)
                        {
                            pset(x + i, y + j, color);
                        }
                    }
                }
            }

            // message box
            if (message != null)
            {
                float box_w = Res.X / 2.0f;
                if (message.body_with_breaks.Count == 1)
                {
                    box_w = message.body_with_breaks[0].Length * 4;
                }
                float box_h = 6 * message.body_with_breaks.Count + 4;

                float x = (Res.X / 2) - (box_w / 2);
                float y = (Res.Y / 2) - (box_h / 2);

                rectfill(x, y, x + box_w, y + box_h, 0);
                rect(x + 1, y + 1, x + 1 + box_w - 2, y + 1 + box_h - 2, 7);

                for (int i = 0; i < message.body_with_breaks.Count; i++)
                {
                    print(message.body_with_breaks[i], x + 3, (y + 3) + (i * 6), 7);
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
            return current_map;
        }

        public override Dictionary<int, string> GetMusicPaths()
        {
            return new Dictionary<int, string>();
        }

        public override List<string> GetSheetPath()
        {
            return new List<string>() { @"raw\platformer_sheet", @"raw\platformer_sheet_1"};
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

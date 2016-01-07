require 'rubygems'
require 'open-uri'
require 'nokogiri'
require 'time'

yesterday = Time.now - 86400 # Seconds in a day

month = 1 # yesterday.month
day = 5 # yesterday.day
year = 2016 # yesterday.year

page = Nokogiri::HTML(open("http://www.hockey-reference.com/friv/dailyleaders.cgi?month=#{month}&day=#{day}&year=#{year}"))

skaters = page.css('#skaters').css('tbody')
goalies = page.css('#goalies').css('tbody')

skater_hashes = []
goalie_hashes = []

skaters.css('tr').each do |skater_row|

  data_hash = {
    name: skater_row.css('td')[1].content,
    position: skater_row.css('td')[2].content,
    team: skater_row.css('td')[3].content,
    game_location: skater_row.css('td')[4].content,
    opponent: skater_row.css('td')[5].content,
    game_result: skater_row.css('td')[6].content,
    goals: skater_row.css('td')[7].content,
    assists: skater_row.css('td')[8].content,
    points: skater_row.css('td')[9].content,
    plus_minus: skater_row.css('td')[10].content,
    pim: skater_row.css('td')[11].content,
    goals_ev: skater_row.css('td')[12].content,
    goals_pp: skater_row.css('td')[13].content,
    goals_sh: skater_row.css('td')[14].content,
    goals_gw: skater_row.css('td')[15].content,
    assists_ev: skater_row.css('td')[16].content,
    assists_pp: skater_row.css('td')[17].content,
    assists_sh: skater_row.css('td')[18].content,
    shots: skater_row.css('td')[19].content,
    shot_pct: skater_row.css('td')[20].content,
    shifts: skater_row.css('td')[21].content,
    toi: skater_row.css('td')[22].content
  }

  skater_hashes << data_hash

end

goalies.css('tr').each do |goalie_row|

  data_hash = {
    name: goalie_row.css('td')[1].content,
    position: goalie_row.css('td')[2].content,
    team: goalie_row.css('td')[3].content,
    game_location: goalie_row.css('td')[4].content,
    opponent: goalie_row.css('td')[5].content,
    game_result: goalie_row.css('td')[6].content,
    goalie_decision: goalie_row.css('td')[7].content,
    goals_allowed: goalie_row.css('td')[8].content,
    shots_faced: goalie_row.css('td')[9].content,
    saves: goalie_row.css('td')[10].content,
    save_pct: goalie_row.css('td')[11].content,
    shutout: goalie_row.css('td')[12].content,
    pim: goalie_row.css('td')[13].content,
    toi: goalie_row.css('td')[14].content,
  }

  goalie_hashes << data_hash
end

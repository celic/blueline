# == Schema Information
#
# Table name: player_stats
#
#  id              :integer          not null, primary key
#  player_id       :integer
#  goals           :integer
#  assists         :integer
#  points          :integer
#  plusminus       :integer
#  pim             :integer
#  evg             :integer
#  ppg             :integer
#  shg             :integer
#  gwg             :integer
#  eva             :integer
#  ppa             :integer
#  sha             :integer
#  shots           :integer
#  shot_percentage :decimal(5, 2)
#  shifts          :integer
#  toi             :integer
#  game_id         :integer
#
# Indexes
#
#  index_player_stats_on_game_id    (game_id)
#  index_player_stats_on_player_id  (player_id)
#

FactoryGirl.define do
  factory :player_stat do
    
  end

end
